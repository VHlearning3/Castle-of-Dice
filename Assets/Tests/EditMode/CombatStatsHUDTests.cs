#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Combat stat readouts: enemy cards under the zone banner and the hero stats block in the
    /// enlarged Hero_Status_Card.
    /// </summary>
    [TestFixture]
    public class CombatStatsHUDTests
    {
        private GameObject hudGo;
        private CombatStatsHUD statsHUD;
        private RectTransform heroCard;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private PlayerDataSO previousSession;

        [SetUp]
        public void SetUp()
        {
            previousSession = PlayerDataSO.Session;
            PlayerDataSO.Session = null;

            hudGo = new GameObject("Test_PlayerHUD", typeof(RectTransform));
            GameObject cardGo = new GameObject("Hero_Status_Card", typeof(RectTransform));
            cardGo.transform.SetParent(hudGo.transform, false);
            heroCard = cardGo.GetComponent<RectTransform>();
            heroCard.sizeDelta = new Vector2(460f, CombatStatsHUD.HeroCardHeight);

            statsHUD = hudGo.AddComponent<CombatStatsHUD>();
            statsHUD.Build(heroCard, null, null, null, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            spawned.Clear();
            if (hudGo != null) Object.DestroyImmediate(hudGo);
            PlayerDataSO.Session = previousSession;
        }

        private EnemyUnit SpawnEnemy(string name, int hp, int ac, int damage, int bonus)
        {
            GameObject go = new GameObject("Test_" + name);
            spawned.Add(go);
            go.AddComponent<StatusEffectController>();
            EnemyUnit enemy = go.AddComponent<EnemyUnit>();
            enemy.ConfigureStats(name, hp, ac, damage, bonus);
            return enemy;
        }

        private PlayerUnit SpawnWarrior()
        {
            GameObject go = new GameObject("Test_Hero");
            spawned.Add(go);
            go.AddComponent<StatusEffectController>();
            PlayerUnit player = go.AddComponent<PlayerUnit>();
            CharacterClassSO warrior = ScriptableObject.CreateInstance<CharacterClassSO>();
            warrior.Initialize(CharacterClassType.Warrior, "Sir Roland", "Frontline", 30, 14, 4, 3, new List<AbilitySO>());
            player.SetCharacterClass(warrior);
            return player;
        }

        private string CardText(int card, string path)
        {
            Transform t = statsHUD.EnemyStrip.Find("Foe_Card_" + card + "/" + path);
            Assert.IsNotNull(t, $"Foe_Card_{card}/{path} missing");
            return t.GetComponent<TMP_Text>().text;
        }

        private string HeroValue(string cell)
        {
            Transform t = statsHUD.HeroPanel.Find(cell + "/Value");
            Assert.IsNotNull(t, cell + "/Value missing");
            return t.GetComponent<TMP_Text>().text;
        }

        [Test]
        public void Build_PlacesStripUnderZoneBanner_AndPanelInsideHeroCard()
        {
            Assert.AreEqual(hudGo.transform, statsHUD.EnemyStrip.parent);
            Assert.AreEqual(new Vector2(0.5f, 1f), statsHUD.EnemyStrip.anchorMin, "Strip is centered at the top");
            Assert.Less(statsHUD.EnemyStrip.anchoredPosition.y, -64f, "Strip starts below the zone banner");
            Assert.AreEqual(heroCard, statsHUD.HeroPanel.parent);
            Assert.IsFalse(statsHUD.EnemyStrip.gameObject.activeSelf, "No battle yet, so no enemy cards");
        }

        [Test]
        public void Build_Twice_DoesNotDuplicateHierarchy()
        {
            statsHUD.RefreshEnemies(new List<CombatUnit> { SpawnEnemy("Skeleton", 12, 13, 4, 2) }, null, true);
            statsHUD.Build(heroCard, null, null, null, null, null);
            statsHUD.RefreshEnemies(new List<CombatUnit> { SpawnEnemy("Zombie", 15, 10, 5, 1) }, null, true);

            int strips = 0;
            foreach (Transform child in hudGo.transform)
            {
                if (child.name == "Enemy_Stats_Strip") strips++;
            }
            Assert.AreEqual(1, strips);
            Assert.AreEqual(1, statsHUD.EnemyStrip.childCount, "The existing card is reused");
            Assert.AreEqual(1, heroCard.childCount, "One Hero_Stats_Panel");
        }

        [Test]
        public void EnemyCard_ShowsNameHpAcAndAttackProfile()
        {
            EnemyUnit skeleton = SpawnEnemy("Skeleton", 12, 13, 4, 2);
            statsHUD.RefreshEnemies(new List<CombatUnit> { skeleton }, null, true);

            Assert.IsTrue(statsHUD.EnemyStrip.gameObject.activeSelf);
            Assert.AreEqual(1, statsHUD.ShownEnemyCount);
            Assert.AreEqual("Skeleton", CardText(0, "Foe_Name_Text"));
            Assert.AreEqual("HP 12 / 12", CardText(0, "Foe_Vitals_Bar/Foe_Vitals_Text"));
            Assert.AreEqual("AC 13", CardText(0, "Foe_AC_Badge/Foe_AC_Text"));
            string stats = CardText(0, "Foe_Stats_Text");
            StringAssert.Contains("+2", stats);
            StringAssert.Contains("Dmg</color> 4", stats);
            StringAssert.Contains("Reach</color> 1", stats);
        }

        [Test]
        public void EnemyCard_UpdatesHpAndEffectsLive()
        {
            EnemyUnit skeleton = SpawnEnemy("Skeleton", 12, 13, 4, 2);
            var roster = new List<CombatUnit> { skeleton };
            statsHUD.RefreshEnemies(roster, null, true);

            skeleton.TakeDamage(5);
            skeleton.StatusEffects.ApplyEffect(StatusEffectType.Poison, 2);
            statsHUD.RefreshEnemies(roster, null, true);

            Assert.AreEqual("HP 7 / 12", CardText(0, "Foe_Vitals_Bar/Foe_Vitals_Text"));
            StringAssert.Contains("Poisoned 2", CardText(0, "Foe_Effects_Text"));
            RectTransform fill = (RectTransform)statsHUD.EnemyStrip.Find("Foe_Card_0/Foe_Vitals_Bar/Foe_Vitals_Fill");
            Assert.AreEqual(7f / 12f, fill.anchorMax.x, 0.001f);
        }

        [Test]
        public void DeadEnemy_DropsOutOfStrip()
        {
            EnemyUnit a = SpawnEnemy("Skeleton", 12, 13, 4, 2);
            EnemyUnit b = SpawnEnemy("Zombie", 15, 10, 5, 1);
            var roster = new List<CombatUnit> { a, b };
            statsHUD.RefreshEnemies(roster, null, true);
            Assert.AreEqual(2, statsHUD.ShownEnemyCount);

            a.TakeDamage(99);
            statsHUD.RefreshEnemies(roster, null, true);

            Assert.AreEqual(1, statsHUD.ShownEnemyCount);
            Assert.AreEqual("Zombie", CardText(0, "Foe_Name_Text"));
            Assert.IsFalse(statsHUD.EnemyStrip.Find("Foe_Card_1").gameObject.activeSelf);
        }

        [Test]
        public void Strip_HidesWhenCombatEnds_AndIgnoresPlayers()
        {
            PlayerUnit hero = SpawnWarrior();
            EnemyUnit skeleton = SpawnEnemy("Skeleton", 12, 13, 4, 2);
            var roster = new List<CombatUnit> { hero, skeleton };

            statsHUD.RefreshEnemies(roster, hero, true);
            Assert.AreEqual(1, statsHUD.ShownEnemyCount, "Only enemies get cards");

            statsHUD.RefreshEnemies(roster, hero, false);
            Assert.IsFalse(statsHUD.EnemyStrip.gameObject.activeSelf);
        }

        [Test]
        public void HeroPanel_ShowsAllHeroStats_AndTracksChanges()
        {
            PlayerUnit hero = SpawnWarrior();
            statsHUD.SetPlayer(hero);
            statsHUD.RefreshHero(true);

            Assert.AreEqual("+3", HeroValue("Stat_Cell_Hit"));
            Assert.AreEqual("+0", HeroValue("Stat_Cell_Weapon"));
            Assert.AreEqual("14", HeroValue("Stat_Cell_Armor"));
            Assert.AreEqual("4", HeroValue("Stat_Cell_Move"));
            StringAssert.Contains("Max HP</color> 30", statsHUD.HeroPanel.Find("Hero_Stats_Detail_Text").GetComponent<TMP_Text>().text);
            StringAssert.Contains("none", statsHUD.HeroPanel.Find("Hero_Effects_Text").GetComponent<TMP_Text>().text);

            hero.AddWeaponDamageBonus(1);
            hero.AddArmorClassBonus(1);
            hero.StatusEffects.ApplyEffect(StatusEffectType.ShieldWall, 1);
            statsHUD.RefreshHero();

            Assert.AreEqual("+1", HeroValue("Stat_Cell_Weapon"));
            Assert.AreEqual("19", HeroValue("Stat_Cell_Armor"), "14 base + 1 armor upgrade + 4 Shield Wall");
            StringAssert.Contains("Shield Wall 1", statsHUD.HeroPanel.Find("Hero_Effects_Text").GetComponent<TMP_Text>().text);
            StringAssert.Contains("Base AC</color> 14", statsHUD.HeroPanel.Find("Hero_Stats_Detail_Text").GetComponent<TMP_Text>().text);
        }

        [Test]
        public void EveryStatusEffect_IsListedWithItsLabel()
        {
            EnemyUnit dummy = SpawnEnemy("Dummy", 10, 10, 1, 0);
            var sb = new System.Text.StringBuilder();
            foreach (StatusEffectType type in System.Enum.GetValues(typeof(StatusEffectType)))
            {
                if (type == StatusEffectType.None) continue;
                dummy.StatusEffects.ApplyEffect(type, 3);
                sb.Clear();
                CombatStatsHUD.AppendEffects(sb, dummy.StatusEffects);
                StringAssert.Contains(CombatStatsHUD.EffectLabel(type) + " 3", sb.ToString(), $"{type} is not shown on the HUD");
                dummy.StatusEffects.RemoveEffect(type);
            }
        }
    }
}
#endif
