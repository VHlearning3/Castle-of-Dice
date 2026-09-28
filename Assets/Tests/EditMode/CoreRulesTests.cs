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
    /// Rule-level regression tests: d20 resolution, buff timing, combat end, progression persistence.
    /// </summary>
    [TestFixture]
    public class CoreRulesTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private PlayerDataSO sessionData;

        [SetUp]
        public void SetUp()
        {
            DiceSystem.ClearSubscribers();
            sessionData = ScriptableObject.CreateInstance<PlayerDataSO>();
            PlayerDataSO.Session = sessionData;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            spawned.Clear();

            SetStaticProperty(typeof(TurnManager), "Instance", null);
            PlayerDataSO.Session = null;
            if (sessionData != null) Object.DestroyImmediate(sessionData);
            DiceSystem.ResetRandom();
        }

        #region D20 Resolution

        [Test]
        public void Natural20_AlwaysHits_AndIsCritical()
        {
            DiceResult result = DiceSystem.EvaluateRoll(20, bonus: -5, targetDC: 30, triggerEvent: false);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.IsCriticalSuccess);
        }

        [Test]
        public void Natural1_AlwaysMisses_EvenWithHugeBonus()
        {
            DiceResult result = DiceSystem.EvaluateRoll(1, bonus: 50, targetDC: 10, triggerEvent: false);
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.IsCriticalFail);
        }

        [Test]
        public void TotalMeetingDC_Hits_OneBelow_Misses()
        {
            Assert.IsTrue(DiceSystem.EvaluateRoll(10, bonus: 4, targetDC: 14, triggerEvent: false).IsSuccess);
            Assert.IsFalse(DiceSystem.EvaluateRoll(10, bonus: 3, targetDC: 14, triggerEvent: false).IsSuccess);
        }

        [Test]
        public void RollDice_D20_CoversFullInclusiveRange()
        {
            DiceSystem.SetSeed(1234);
            bool sawOne = false, sawTwenty = false;
            for (int i = 0; i < 2000; i++)
            {
                int roll = DiceSystem.RollDice(20);
                Assert.That(roll, Is.InRange(1, 20));
                sawOne |= roll == 1;
                sawTwenty |= roll == 20;
            }
            Assert.IsTrue(sawOne && sawTwenty, "A d20 must be able to roll both 1 and 20.");
        }

        [Test]
        public void Advantage_KeepsHigher_Disadvantage_KeepsLower()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                DiceSystem.SetSeed(seed);
                int a = DiceSystem.RollDice(20);
                int b = DiceSystem.RollDice(20);

                DiceSystem.SetSeed(seed);
                Assert.AreEqual(Mathf.Max(a, b), DiceSystem.RollD20(0, 10, AdvantageType.Advantage).RawRoll);

                DiceSystem.SetSeed(seed);
                Assert.AreEqual(Mathf.Min(a, b), DiceSystem.RollD20(0, 10, AdvantageType.Disadvantage).RawRoll);
            }
        }

        #endregion

        #region Status Effects

        [Test]
        public void SelfBuffAppliedDuringOwnTurn_SurvivesThatTurnsEnd()
        {
            PlayerUnit rogue = CreateUnit<PlayerUnit>("Rogue");
            TurnManager turnManager = CreateTurnManager();
            SetField(turnManager, "currentActiveUnit", rogue);

            rogue.StatusEffects.ApplyEffect(StatusEffectType.AdvantageNextAttack, 1);
            rogue.StatusEffects.ProcessTurnEndEffects();
            Assert.IsTrue(rogue.StatusEffects.HasEffect(StatusEffectType.AdvantageNextAttack),
                "Shadow Step advantage must still be available on the rogue's next turn.");

            SetField(turnManager, "currentActiveUnit", null);
            rogue.StatusEffects.ProcessTurnEndEffects();
            Assert.IsFalse(rogue.StatusEffects.HasEffect(StatusEffectType.AdvantageNextAttack),
                "The buff expires at the end of the following turn if unused.");
        }

        [Test]
        public void ShieldWall_GrantsPlusFourArmorClass()
        {
            PlayerUnit warrior = CreateUnit<PlayerUnit>("Warrior");
            int baseAC = warrior.ArmorClass;

            warrior.StatusEffects.ApplyEffect(StatusEffectType.ShieldWall, 1);

            Assert.AreEqual(baseAC + StatusEffectController.ShieldWallArmorBonus, warrior.ArmorClass);
        }

        #endregion

        #region Combat Flow

        [Test]
        public void EndCombat_AfterCombatAlreadyConcluded_DoesNotFireAgain()
        {
            TurnManager turnManager = CreateTurnManager();
            SetField(turnManager, "isCombatActive", true);

            int endedCount = 0;
            System.Action<bool> handler = _ => endedCount++;
            TurnManager.OnCombatEnded += handler;
            try
            {
                turnManager.EndCombat(false);
                turnManager.EndCombat(false);
            }
            finally
            {
                TurnManager.OnCombatEnded -= handler;
            }

            Assert.AreEqual(1, endedCount, "Defeat must only be broadcast once (the defeat modal reopened before).");
        }

        [Test]
        public void Revive_RestoresDeadUnitToFullHealth()
        {
            PlayerUnit hero = CreateUnit<PlayerUnit>("Hero");
            hero.TakeDamage(hero.MaxHP + 5);
            Assert.IsFalse(hero.IsAlive);
            Assert.IsFalse(hero.gameObject.activeSelf);

            hero.Revive();

            Assert.IsTrue(hero.IsAlive);
            Assert.IsTrue(hero.gameObject.activeSelf);
            Assert.AreEqual(hero.MaxHP, hero.CurrentHP);
        }

        #endregion

        #region Progression & Save

        [Test]
        public void ApplyProgression_IsIdempotent()
        {
            PlayerUnit hero = CreateUnit<PlayerUnit>("Hero");
            int baseMax = hero.MaxHP;
            int baseAttribute = hero.PrimaryAttributeBonus;

            for (int i = 0; i < 3; i++)
            {
                hero.ApplyProgression(2, maxHpBonus: 5, attributeBonus: 1, weaponBonus: 2, armorBonus: 1, upgradedSlots: null);
            }

            Assert.AreEqual(baseMax + 5, hero.MaxHP);
            Assert.AreEqual(baseAttribute + 1, hero.PrimaryAttributeBonus);
            Assert.AreEqual(2, hero.WeaponDamageBonus);
            Assert.AreEqual(1, hero.ArmorClassBonus);
        }

        [Test]
        public void UpgradesPushedToSession_RoundTripOntoFreshHero()
        {
            PlayerUnit hero = CreateUnit<PlayerUnit>("Hero");
            hero.ApplyProgression(1, 0, 0, 0, 0, null); // establish the class baseline
            hero.AddWeaponDamageBonus(1);
            hero.AddArmorClassBonus(2);
            hero.ApplyHeroResilience(5);

            Assert.AreEqual(1, sessionData.WeaponDamageBonus);
            Assert.AreEqual(2, sessionData.ArmorClassBonus);
            Assert.AreEqual(5, sessionData.MaxHPBonus);

            // A new zone scene spawns a fresh hero prefab: the session store must restore the same stats
            PlayerUnit nextSceneHero = CreateUnit<PlayerUnit>("NextSceneHero");
            int freshMax = nextSceneHero.MaxHP;
            sessionData.ApplyToPlayer(nextSceneHero);
            sessionData.ApplyToPlayer(nextSceneHero);

            Assert.AreEqual(1, nextSceneHero.WeaponDamageBonus);
            Assert.AreEqual(2, nextSceneHero.ArmorClassBonus);
            Assert.AreEqual(freshMax + 5, nextSceneHero.MaxHP);
        }

        [Test]
        public void RestoreFromSave_CanLowerGoldBelowCurrent()
        {
            InventoryManager inventory = CreateUnit<InventoryManager>("Inventory", runAwake: false); // Awake calls DontDestroyOnLoad (play mode only)
            inventory.AddGold(500);

            inventory.RestoreFromSave(gold: 30, scrapMetal: 2, rerollScrolls: 0);

            Assert.AreEqual(30, inventory.CurrentGold);
            Assert.AreEqual(2, inventory.ScrapMetalCount);
            Assert.AreEqual(0, inventory.RerollScrollCount);
        }

        #endregion

        #region Helpers

        private T CreateUnit<T>(string name, bool runAwake = true) where T : Component
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

        private TurnManager CreateTurnManager()
        {
            TurnManager turnManager = CreateUnit<TurnManager>("TurnManager");
            SetStaticProperty(typeof(TurnManager), "Instance", turnManager);
            return turnManager;
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
