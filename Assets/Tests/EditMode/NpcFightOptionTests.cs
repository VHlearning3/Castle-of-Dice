#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// The [Fight] choice in village NPC dialogue and the brawl it starts on the spot.
    /// </summary>
    [TestFixture]
    public class NpcFightOptionTests
    {
        private readonly List<Object> spawned = new List<Object>();

        private GridManager gridManager;
        private TurnManager turnManager;
        private PlayerUnit player;
        private VillageNPC npc;

        [TearDown]
        public void TearDown()
        {
            if (NpcBrawlerUnit.Active != null)
            {
                NpcBrawlerUnit.Active.Recover();
            }

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
        }

        #region Dialogue Options

        [Test]
        public void ComposeOptions_InsertsFightBeforeTrailingExit()
        {
            DialogueOption lore = new DialogueOption("[Lore] Tell me about the castle.", null);
            DialogueOption exit = new DialogueOption("[Exit] Farewell.", null, false, 10, "", null, "[ACTION_CLOSE_DIALOGUE]");
            DialogueNodeSO node = CreateNode(lore, exit);
            DialogueOption fight = VillageNPC.CreateFightOption();

            List<DialogueOption> options = DialogueController.ComposeOptions(node, new List<DialogueOption> { fight });

            CollectionAssert.AreEqual(new[] { lore, fight, exit }, options, "[Fight] must sit just above the exit choice.");
        }

        [Test]
        public void ComposeOptions_AppendsFightWhenNodeHasNoExit()
        {
            DialogueOption accept = new DialogueOption("I'll purge your cellar right away.", null);
            DialogueOption refuse = new DialogueOption("Rats are beneath me. Find someone else.", null);
            DialogueNodeSO node = CreateNode(accept, refuse);
            DialogueOption fight = VillageNPC.CreateFightOption();

            List<DialogueOption> options = DialogueController.ComposeOptions(node, new List<DialogueOption> { fight });

            CollectionAssert.AreEqual(new[] { accept, refuse, fight }, options);
        }

        [Test]
        public void ComposeOptions_LeavesNodesWithoutChoicesEmptySoContinueStillShows()
        {
            DialogueNodeSO node = CreateNode();

            List<DialogueOption> options = DialogueController.ComposeOptions(node, new List<DialogueOption> { VillageNPC.CreateFightOption() });

            Assert.AreEqual(0, options.Count);
        }

        [Test]
        public void ComposeOptions_WithoutExtrasReturnsNodeOptionsUnchanged()
        {
            DialogueOption lore = new DialogueOption("[Lore] Tell me about the castle.", null);
            DialogueNodeSO node = CreateNode(lore);

            CollectionAssert.AreEqual(new[] { lore }, DialogueController.ComposeOptions(node, null));
        }

        [Test]
        public void FightOption_EndsConversationWithoutSkillCheck()
        {
            DialogueOption fight = VillageNPC.CreateFightOption();

            StringAssert.StartsWith("[Fight]", fight.OptionText);
            Assert.IsFalse(fight.RequiresCheck, "Picking a fight must not need a D20 check.");
            Assert.IsNull(fight.NextNodeSuccess, "Picking a fight must close the dialogue.");
            Assert.AreEqual(VillageNPC.FightActionTag, fight.CombatDebuffTag);
            Assert.IsFalse(DialogueController.IsExitOption(fight));
        }

        [Test]
        public void CanOfferFight_OnlyInTheStartingVillage()
        {
            Assert.IsTrue(VillageNPC.CanOfferFight(true, "Zone_1_VillageAndCellar"));
            Assert.IsFalse(VillageNPC.CanOfferFight(true, "Zone_3_CastleCourtyard"), "Boss challengers in castle zones must not get a [Fight] choice.");
            Assert.IsFalse(VillageNPC.CanOfferFight(false, "Zone_1_VillageAndCellar"), "The per-NPC toggle must hide the choice.");
        }

        #endregion

        #region Brawl

        [Test]
        public void Begin_StartsCombatAgainstTheNpcWithItsBrawlStats()
        {
            SetUpBattlefield();

            NpcBrawlerUnit brawler = NpcBrawlerUnit.Begin(npc, player);

            Assert.IsNotNull(brawler);
            Assert.AreSame(brawler, NpcBrawlerUnit.Active);
            Assert.IsTrue(turnManager.IsCombatActive, "Choosing [Fight] must start turn-based combat.");
            Assert.AreEqual(2, turnManager.ActiveUnits.Count, "The brawl is the hero against that one NPC.");
            Assert.AreEqual("Barnaby", brawler.UnitName);
            Assert.AreEqual(npc.BrawlMaxHP, brawler.MaxHP);
            Assert.AreEqual(npc.BrawlArmorClass, brawler.ArmorClass);
            Assert.AreEqual(npc.BrawlDamage, brawler.AttackDamage);
            Assert.Less(brawler.MaxHP, 25, "A villager must not count as an elite.");
            Assert.IsFalse(npc.IsInteractable, "The NPC cannot be talked to mid-fight.");
            Assert.AreEqual(144, gridManager.Tiles.Count, "The brawl uses the standard 12x12 grid.");
            Assert.AreNotEqual(player.GridPosition, brawler.GridPosition);
        }

        [Test]
        public void Begin_RefusesASecondFightWhileOneIsRunning()
        {
            SetUpBattlefield();
            NpcBrawlerUnit.Begin(npc, player);

            GameObject otherGo = Track(new GameObject("Mirabel"));
            VillageNPC other = otherGo.AddComponent<VillageNPC>();

            Assert.IsNull(NpcBrawlerUnit.Begin(other, player));
            Assert.IsNull(otherGo.GetComponent<NpcBrawlerUnit>());
        }

        [Test]
        public void BeatenNpc_IsKnockedOutNotRemoved_ThenRecoversAndCanTalkAgain()
        {
            SetUpBattlefield();
            Vector3 home = npc.transform.position;
            NpcBrawlerUnit brawler = NpcBrawlerUnit.Begin(npc, player);

            brawler.TakeDamage(999);

            Assert.IsTrue(brawler.IsKnockedOut);
            Assert.IsFalse(brawler.IsAlive);
            Assert.IsTrue(npc.gameObject.activeSelf, "A beaten villager stays in the village.");
            Assert.IsFalse(turnManager.IsCombatActive, "Knocking the NPC out wins the fight.");
            Assert.AreEqual(0, gridManager.Tiles.Count, "The brawl grid is cleared after the win.");

            brawler.Recover();

            Assert.IsNull(npc.GetComponent<NpcBrawlerUnit>(), "The NPC must not stay a combatant after the fight.");
            Assert.IsNull(npc.GetComponent<StatusEffectController>());
            Assert.IsNull(NpcBrawlerUnit.Active);
            Assert.IsTrue(npc.IsInteractable, "Dialogue and shop must keep working after the fight.");
            Assert.AreEqual(home, npc.transform.position);
        }

        [Test]
        public void LostFight_TryAgainRestartsTheBrawlAtFullHealth()
        {
            SetUpBattlefield();
            NpcBrawlerUnit brawler = NpcBrawlerUnit.Begin(npc, player);
            brawler.TakeDamage(5);

            player.TakeDamage(999);
            Assert.IsFalse(turnManager.IsCombatActive, "Losing ends the fight like any other.");
            Assert.AreSame(brawler, NpcBrawlerUnit.Active, "The brawl waits on the Defeat modal.");

            player.Revive();
            brawler.Restart(player);

            Assert.IsTrue(turnManager.IsCombatActive);
            Assert.AreEqual(brawler.MaxHP, brawler.CurrentHP);
        }

        #endregion

        #region Helpers

        private void SetUpBattlefield()
        {
            GameObject gridGo = Track(new GameObject("Test_GridManager"));
            gridManager = gridGo.AddComponent<GridManager>();
            GridManager.Instance = gridManager;

            GameObject turnGo = Track(new GameObject("Test_TurnManager"));
            turnManager = turnGo.AddComponent<TurnManager>();

            GameObject playerGo = Track(new GameObject("Test_PlayerUnit"));
            playerGo.transform.position = new Vector3(300f, 0f, 300f);
            playerGo.AddComponent<StatusEffectController>();
            player = playerGo.AddComponent<PlayerUnit>();
            player.InitializeUnit();

            GameObject npcGo = Track(new GameObject("NPC_Barnaby"));
            npcGo.transform.position = new Vector3(303f, 0f, 300f);
            CapsuleCollider capsule = npcGo.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1.5f, 0f);
            capsule.height = 3f;
            npc = npcGo.AddComponent<VillageNPC>();
            SetNpcName(npc, "Barnaby");
        }

        private static void SetNpcName(VillageNPC target, string npcName)
        {
            typeof(VillageNPC)
                .GetField("npcName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(target, npcName);
        }

        private DialogueNodeSO CreateNode(params DialogueOption[] options)
        {
            DialogueNodeSO node = ScriptableObject.CreateInstance<DialogueNodeSO>();
            Track(node);
            node.Initialize("Tester", "Hello.");
            node.SetOptions(new List<DialogueOption>(options));
            return node;
        }

        private T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        #endregion
    }
}
#endif
