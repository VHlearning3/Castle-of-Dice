#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    [TestFixture]
    public class LockpickMinigameTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            if (LockpickMinigameUI.Instance != null)
            {
                Object.DestroyImmediate(LockpickMinigameUI.Instance.gameObject);
            }

            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            }
            created.Clear();
            GameInput.SetExplorationInputEnabled(true);
        }

        #region Minigame Logic

        [Test]
        public void HigherDC_NarrowsGoldZone_AndSpeedsUpPick()
        {
            Assert.Greater(LockpickMinigame.SweetZoneWidthForDC(8), LockpickMinigame.SweetZoneWidthForDC(13));
            Assert.Greater(LockpickMinigame.SweetZoneWidthForDC(13), LockpickMinigame.SweetZoneWidthForDC(20));
            Assert.Less(LockpickMinigame.MarkerSpeedForDC(8), LockpickMinigame.MarkerSpeedForDC(13));
            Assert.Greater(LockpickMinigame.SweetZoneWidthForDC(30), 0f, "Even the hardest lock keeps a pickable gold zone.");
        }

        [Test]
        public void Tick_BouncesMarkerInsideBar()
        {
            var game = new LockpickMinigame(13, random: new System.Random(1));
            for (int i = 0; i < 500; i++)
            {
                game.Tick(0.137f);
                Assert.That(game.MarkerPosition, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void SettingEveryPin_UnlocksTheLock()
        {
            var game = new LockpickMinigame(13, pinCount: 3, random: new System.Random(7));

            Assert.AreEqual(LockpickPressResult.PinSet, PressInZone(game));
            Assert.AreEqual(LockpickPressResult.PinSet, PressInZone(game));
            Assert.AreEqual(LockpickPressResult.Unlocked, PressInZone(game));

            Assert.IsTrue(game.IsFinished);
            Assert.IsTrue(game.IsUnlocked);
            Assert.AreEqual(3, game.PinsSet);
            Assert.AreEqual(0, game.Slips);
        }

        [Test]
        public void EachSetPin_MakesThePickFaster()
        {
            var game = new LockpickMinigame(13, random: new System.Random(3));
            float first = game.MarkerSpeed;
            PressInZone(game);
            Assert.Greater(game.MarkerSpeed, first);
        }

        [Test]
        public void MissingTheZone_Slips_AndTooManySlipsBreaksTheAttempt()
        {
            var game = new LockpickMinigame(13, maxSlips: 2, random: new System.Random(11));

            Assert.AreEqual(LockpickPressResult.Slipped, PressOutsideZone(game));
            Assert.IsFalse(game.IsFinished);
            Assert.AreEqual(LockpickPressResult.Broken, PressOutsideZone(game));

            Assert.IsTrue(game.IsFinished);
            Assert.IsFalse(game.IsUnlocked);
            Assert.AreEqual(LockpickPressResult.Ignored, game.Press(), "A finished attempt ignores further presses.");
        }

        [Test]
        public void Slip_KeepsSetPins()
        {
            var game = new LockpickMinigame(13, maxSlips: 3, random: new System.Random(5));
            PressInZone(game);
            PressOutsideZone(game);
            Assert.AreEqual(1, game.PinsSet);
            Assert.AreEqual(1, game.Slips);
        }

        private static LockpickPressResult PressInZone(LockpickMinigame game)
        {
            for (int i = 0; i < 5000 && !game.IsMarkerInSweetZone; i++) game.Tick(0.005f);
            Assert.IsTrue(game.IsMarkerInSweetZone, "Marker never reached the gold zone.");
            return game.Press();
        }

        private static LockpickPressResult PressOutsideZone(LockpickMinigame game)
        {
            for (int i = 0; i < 5000 && game.IsMarkerInSweetZone; i++) game.Tick(0.005f);
            Assert.IsFalse(game.IsMarkerInSweetZone, "Marker never left the gold zone.");
            return game.Press();
        }

        #endregion

        #region Lock Interaction

        [Test]
        public void NonRogue_GetsCantLockpickPopup_AndLockStaysShut()
        {
            LockpickInteraction lockpick = CreateLock(out _);
            PlayerUnit warrior = CreatePlayer(CharacterClassType.Warrior);
            int hpBefore = warrior.CurrentHP;

            lockpick.Interact(warrior);

            Assert.IsTrue(lockpick.IsLocked);
            Assert.IsFalse(LockpickMinigameUI.IsOpen, "Only the Rogue may start the minigame.");
            Assert.IsNotNull(LockpickMinigameUI.Instance);
            Assert.AreEqual(LockpickInteraction.CannotLockpickMessage, LockpickMinigameUI.Instance.VisibleToastMessage);
            Assert.AreEqual(hpBefore, warrior.CurrentHP, "No trap for a class that cannot even try.");
        }

        [Test]
        public void Mage_CannotLockpickEither()
        {
            Assert.IsFalse(LockpickInteraction.CanPickLocks(CreatePlayer(CharacterClassType.Mage)));
            Assert.IsTrue(LockpickInteraction.CanPickLocks(CreatePlayer(CharacterClassType.Rogue)));
        }

        [Test]
        public void Rogue_OpensMinigame_AndFreezesExploration()
        {
            LockpickInteraction lockpick = CreateLock(out _);
            PlayerUnit rogue = CreatePlayer(CharacterClassType.Rogue);

            lockpick.Interact(rogue);

            Assert.IsTrue(LockpickMinigameUI.IsOpen);
            Assert.IsFalse(GameInput.IsExplorationInputEnabled);
            Assert.IsTrue(lockpick.IsLocked, "The lock opens only when the minigame is won.");
        }

        [Test]
        public void WinningMinigame_OpensLock_AndRevealsSecretPath()
        {
            LockpickInteraction lockpick = CreateLock(out GameObject hiddenPath);
            PlayerUnit rogue = CreatePlayer(CharacterClassType.Rogue);

            FinishMinigame(lockpick, rogue, LockpickOutcome.Unlocked);

            Assert.IsFalse(lockpick.IsLocked);
            Assert.IsTrue(hiddenPath.activeSelf);
        }

        [Test]
        public void LosingMinigame_SpringsTrap_AndKeepsLockShut()
        {
            LockpickInteraction lockpick = CreateLock(out GameObject hiddenPath);
            PlayerUnit rogue = CreatePlayer(CharacterClassType.Rogue);
            int hpBefore = rogue.CurrentHP;

            FinishMinigame(lockpick, rogue, LockpickOutcome.Failed);

            Assert.IsTrue(lockpick.IsLocked);
            Assert.IsFalse(hiddenPath.activeSelf);
            Assert.AreEqual(hpBefore - 4, rogue.CurrentHP);
        }

        [Test]
        public void SteppingAway_HasNoPenalty()
        {
            LockpickInteraction lockpick = CreateLock(out _);
            PlayerUnit rogue = CreatePlayer(CharacterClassType.Rogue);
            int hpBefore = rogue.CurrentHP;

            FinishMinigame(lockpick, rogue, LockpickOutcome.Cancelled);

            Assert.IsTrue(lockpick.IsLocked);
            Assert.AreEqual(hpBefore, rogue.CurrentHP);
        }

        private static void FinishMinigame(LockpickInteraction lockpick, PlayerUnit player, LockpickOutcome outcome)
        {
            MethodInfo finish = typeof(LockpickInteraction).GetMethod("HandleMinigameFinished", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(finish, "LockpickInteraction.HandleMinigameFinished not found.");
            finish.Invoke(lockpick, new object[] { player, outcome });
        }

        private LockpickInteraction CreateLock(out GameObject hiddenPath)
        {
            GameObject gate = new GameObject("Test_Lock_Gate");
            created.Add(gate);
            LockpickInteraction lockpick = gate.AddComponent<LockpickInteraction>();

            hiddenPath = new GameObject("Test_Hidden_Door");
            created.Add(hiddenPath);
            hiddenPath.SetActive(false);

            SerializedObject so = new SerializedObject(lockpick);
            so.FindProperty("lockpickDC").intValue = 13;
            so.FindProperty("rewardGold").intValue = 0;
            so.FindProperty("hiddenPathObject").objectReferenceValue = hiddenPath;
            so.FindProperty("hasTrap").boolValue = true;
            so.FindProperty("trapDamage").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();
            return lockpick;
        }

        private PlayerUnit CreatePlayer(CharacterClassType classType)
        {
            GameObject go = new GameObject("Test_Player_" + classType);
            created.Add(go);
            go.AddComponent<StatusEffectController>();
            PlayerUnit player = go.AddComponent<PlayerUnit>();
            player.InitializeUnit();

            CharacterClassSO cls = ScriptableObject.CreateInstance<CharacterClassSO>();
            created.Add(cls);
            var abilities = new List<AbilitySO> { ScriptableObject.CreateInstance<AbilitySO>() };
            created.Add(abilities[0]);
            cls.Initialize(classType, classType.ToString(), "Test", 30, 13, 4, 3, abilities);
            player.SetCharacterClass(cls);
            return player;
        }

        #endregion
    }
}
#endif
