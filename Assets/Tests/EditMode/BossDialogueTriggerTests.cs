using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Boss intro dialogues play only when the hero walks into the boss's combat trigger,
    /// and combat follows when the conversation ends.
    /// </summary>
    [TestFixture]
    public class BossDialogueTriggerTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private DungeonRoomController room;
        private VillageNPC npc;
        private GameObject boss;

        [SetUp]
        public void SetUp()
        {
            GridManager grid = Track(new GameObject("BossTest_Grid")).AddComponent<GridManager>();
            GridManager.Instance = grid;
            grid.GenerateGridAt(Vector3.zero, 12, 12, 1.6f);

            DialogueNodeSO intro = Track(ScriptableObject.CreateInstance<DialogueNodeSO>());
            intro.Initialize(speaker: "Cursed Commander", text: "Who dares?", portrait: null, isExit: false);
            intro.SetOptions(new List<DialogueOption>
            {
                new DialogueOption("[Fight] Prepare for destruction!", null, false, 10, "", null, "")
            });

            npc = Track(new GameObject("NPC_CursedCommander")).AddComponent<VillageNPC>();
            npc.transform.position = new Vector3(0f, 1f, -12f);
            npc.StartingDialogueNode = intro;
            npc.IsInteractable = true;

            boss = Track(new GameObject("Boss_CursedCommander"));
            boss.transform.position = new Vector3(2f, 1f, 6f);

            room = Track(new GameObject("Courtyard_Encounter_Trigger")).AddComponent<DungeonRoomController>();
            room.roomLocation = "Courtyard";
            room.bossIdentifier = "CursedCommander";
            room.roomEnemies.Add(boss);
            room.bossDialogueNpc = npc;
            room.generateGridOnCombat = false;

            Invoke(room, "Start");
        }

        [TearDown]
        public void TearDown()
        {
            Invoke(room, "OnDestroy");
            if (DialogueController.Instance != null)
            {
                DialogueController.Instance.EndDialogue();
                Object.DestroyImmediate(DialogueController.Instance.gameObject);
            }
            foreach (TurnManager tm in Object.FindObjectsByType<TurnManager>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(tm.gameObject);
            }
            GridManager.Instance = null;

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
        }

        [Test]
        public void BossNpc_CannotBeTalkedTo_BeforeTheTrigger_AndWaitsAtTheBossSpot()
        {
            Assert.IsFalse(npc.IsInteractable, "The boss must not be talked to before the hero reaches the combat trigger.");
            Assert.AreEqual(boss.transform.position.x, npc.transform.position.x, 0.001f);
            Assert.AreEqual(boss.transform.position.z, npc.transform.position.z, 0.001f);
        }

        [Test]
        public void WalkingIn_StartsBossDialogue_AndCombatFollowsWhenItEnds()
        {
            room.BeginEncounter(null);

            Assert.IsTrue(DialogueController.Instance.IsInDialogue, "Walking into the trigger must open the boss dialogue.");
            Assert.IsTrue(room.IsAwaitingBossDialogue);
            Assert.AreEqual(RoomState.Unexplored, room.CurrentState, "Combat waits for the dialogue.");
            Assert.IsFalse(boss.activeSelf);

            DialogueController.Instance.EndDialogue();

            Assert.AreEqual(RoomState.CombatActive, room.CurrentState, "Combat starts as soon as the dialogue ends.");
            Assert.IsTrue(boss.activeSelf);
            Assert.IsFalse(npc.gameObject.activeSelf, "The fighting boss replaces its dialogue stand-in.");
        }

        [Test]
        public void SecondEntry_DoesNotReplayTheDialogue()
        {
            room.BeginEncounter(null);
            DialogueController.Instance.EndDialogue();

            room.BeginEncounter(null);
            Assert.IsFalse(DialogueController.Instance.IsInDialogue);
        }

        private T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        private static void Invoke(object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            m?.Invoke(target, null);
        }
    }
}
