#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Village side quests from acceptance to reward: objective tracking, hand-in, quest-giver dialogue,
    /// head markers, tracker rows, journal and the quest data itself.
    /// </summary>
    [TestFixture]
    public class VillageQuestTests
    {
        private readonly List<Object> spawned = new List<Object>();

        private QuestManager quests;
        private InventoryManager inventory;
        private ItemSO potion;
        private ItemSO greaterPotion;
        private ItemSO ring;
        private ItemSO herb;
        private QuestSO ratQuest;
        private QuestSO ringQuest;
        private QuestSO herbQuest;
        private QuestSO scrapQuest;

        [SetUp]
        public void SetUp()
        {
            GameObject inventoryGo = Track(new GameObject("Test_Inventory"));
            inventory = inventoryGo.AddComponent<InventoryManager>();
            GameObject questGo = Track(new GameObject("Test_Quests"));
            quests = questGo.AddComponent<QuestManager>();

            SetStaticProperty(typeof(InventoryManager), "Instance", inventory);
            SetStaticProperty(typeof(QuestManager), "Instance", quests);

            potion = CreateItem("potion_test", "Health Potion", ItemType.Consumable);
            greaterPotion = CreateItem("greater_test", "Greater Health Potion", ItemType.Consumable);
            ring = CreateItem("ring_test", "Signet Ring", ItemType.QuestItem);
            herb = CreateItem("herb_test", "Swamp Blossom", ItemType.QuestItem);

            ratQuest = CreateQuest("quest_rats", "Cellar Pests", 3, 30, 15, potion);
            ratQuest.ConfigureObjective(QuestObjectiveType.DefeatEnemies, "Slay the giant cellar rats", "Wine cellar", "Barnaby", enemyName: "Cellar Rat");
            ratQuest.ConfigureRewards(2, null);
            ratQuest.ConfigureDialogue("Still rats down there.", "Is it done?", "The cellar is clear.", "Thank you!", "Quiet cellar now.");

            ringQuest = CreateQuest("quest_ring", "The Lost Signet Ring", 1, 50, 0, null);
            ringQuest.ConfigureObjective(QuestObjectiveType.CollectItem, "Recover the ring", "Treasure tower", "Othelia", item: ring);

            herbQuest = CreateQuest("quest_herbs", "Herbs for the Healer", 3, 30, 0, potion);
            herbQuest.ConfigureObjective(QuestObjectiveType.CollectItem, "Gather swamp blossoms", "Forest Path", "Mirabel", item: herb);
            herbQuest.ConfigureRewards(1, greaterPotion);

            scrapQuest = CreateQuest("quest_scrap", "Scrap for the Forge", 5, 50, 0, null);
            scrapQuest.ConfigureObjective(QuestObjectiveType.ScrapMetal, "Collect scrap metal", "Won from fights", "Baldur");

            quests.RegisterQuest(ratQuest);
            quests.RegisterQuest(ringQuest);
            quests.RegisterQuest(herbQuest);
            quests.RegisterQuest(scrapQuest);
        }

        [TearDown]
        public void TearDown()
        {
            SetStaticProperty(typeof(InventoryManager), "Instance", null);
            SetStaticProperty(typeof(QuestManager), "Instance", null);

            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
        }

        #region Quest Flow

        [Test]
        public void StartQuest_Again_KeepsProgressAndState()
        {
            Assert.IsTrue(quests.StartQuest("quest_rats"));
            quests.RecordEnemyDefeated("Giant Cellar Rat");

            Assert.IsFalse(quests.StartQuest("quest_rats"), "Accepting a running quest again must not restart it.");
            Assert.AreEqual(1, quests.GetQuestProgress("quest_rats"));
        }

        [Test]
        public void DefeatEnemies_CountsMatchingKills_EvenBeforeAccepting()
        {
            quests.RecordEnemyDefeated("Giant Cellar Rat");
            quests.RecordEnemyDefeated("Skeleton Guard");

            quests.StartQuest("quest_rats");
            Assert.AreEqual(1, quests.GetQuestProgress("quest_rats"), "A rat slain before Barnaby asked still counts; a skeleton doesn't.");

            quests.RecordEnemyDefeated("Giant Cellar Rat");
            quests.RecordEnemyDefeated("Giant Cellar Rat");
            quests.RecordEnemyDefeated("Giant Cellar Rat");

            Assert.AreEqual(3, quests.GetQuestProgress("quest_rats"), "Progress is capped at the requirement.");
            Assert.IsTrue(quests.IsReadyToTurnIn("quest_rats"));
        }

        [Test]
        public void CollectItem_ProgressFollowsCarriedItems()
        {
            inventory.AddItem(herb, 1);
            quests.StartQuest("quest_herbs");
            Assert.AreEqual(1, quests.GetQuestProgress("quest_herbs"), "Blossoms picked before accepting count at once.");

            inventory.AddItem(herb, 2);
            quests.RefreshTrackedObjectives();
            Assert.AreEqual(3, quests.GetQuestProgress("quest_herbs"));
            Assert.IsTrue(quests.IsReadyToTurnIn("quest_herbs"));
        }

        [Test]
        public void CompleteQuest_FailsUntilObjectiveIsDone()
        {
            quests.StartQuest("quest_ring");
            int goldBefore = inventory.CurrentGold;

            Assert.IsFalse(quests.CompleteQuest("quest_ring"));
            Assert.AreEqual(QuestState.InProgress, quests.GetQuestState("quest_ring"));
            Assert.AreEqual(goldBefore, inventory.CurrentGold);
        }

        [Test]
        public void CompleteQuest_TakesQuestItemsAndPaysReward()
        {
            inventory.AddItem(ring, 1);
            quests.StartQuest("quest_ring");
            int goldBefore = inventory.CurrentGold;

            Assert.IsTrue(quests.CompleteQuest("quest_ring"));

            Assert.AreEqual(QuestState.Completed, quests.GetQuestState("quest_ring"));
            Assert.AreEqual(0, inventory.GetItemCount(ring), "Othelia keeps her ring.");
            Assert.AreEqual(goldBefore + 50, inventory.CurrentGold);
            Assert.IsFalse(quests.CompleteQuest("quest_ring"), "A quest pays out only once.");
            Assert.IsFalse(quests.StartQuest("quest_ring"), "A finished quest can't be taken again.");
        }

        [Test]
        public void CompleteQuest_BonusAddsGoldAndItemAmount()
        {
            quests.SetNegotiatedBonus("quest_rats", true);
            quests.StartQuest("quest_rats");
            for (int i = 0; i < 3; i++) quests.RecordEnemyDefeated("Giant Cellar Rat");
            int goldBefore = inventory.CurrentGold;

            Assert.IsTrue(quests.CompleteQuest("quest_rats"));

            Assert.AreEqual(goldBefore + 45, inventory.CurrentGold, "30 gold + 15 negotiated bonus.");
            Assert.AreEqual(2, inventory.GetItemCount(potion), "Barnaby gives two healing draughts.");
        }

        [Test]
        public void CompleteQuest_BonusItemReplacesRewardItem()
        {
            quests.SetNegotiatedBonus("quest_herbs", true);
            inventory.AddItem(herb, 3);
            quests.StartQuest("quest_herbs");

            Assert.IsTrue(quests.CompleteQuest("quest_herbs"));

            Assert.AreEqual(1, inventory.GetItemCount(greaterPotion), "Mirabel brews a Greater Health Potion after the Nature check.");
            Assert.AreEqual(0, inventory.GetItemCount(potion), "...instead of the usual reward.");
            Assert.AreEqual(0, inventory.GetItemCount(herb));
        }

        [Test]
        public void ScrapQuest_TurnInTakesFiveScrap()
        {
            inventory.AddScrapMetal(7);
            quests.StartQuest("quest_scrap");
            Assert.IsTrue(quests.IsReadyToTurnIn("quest_scrap"));

            Assert.IsTrue(quests.CompleteQuest("quest_scrap"));
            Assert.AreEqual(2, inventory.ScrapMetalCount, "Baldur takes exactly the five pieces he asked for.");
        }

        [Test]
        public void ScrapQuest_SellingScrapBeforeHandIn_BlocksCompletion()
        {
            inventory.AddScrapMetal(5);
            quests.StartQuest("quest_scrap");
            inventory.RemoveScrapMetal(5);

            Assert.IsFalse(quests.CompleteQuest("quest_scrap"));
            Assert.AreEqual(0, quests.GetQuestProgress("quest_scrap"));
        }

        [Test]
        public void NegotiatedBonus_SurvivesSaveCaptureAndRestore()
        {
            quests.SetNegotiatedBonus("quest_rats", true);
            List<string> saved = new List<string>();
            quests.CaptureBonuses(saved);

            quests.ResetAllQuests();
            Assert.IsFalse(quests.HasNegotiatedBonus("quest_rats"));

            quests.RestoreBonuses(saved);
            Assert.IsTrue(quests.HasNegotiatedBonus("quest_rats"));
        }

        [Test]
        public void CaptureState_KeepsEarlyKillsOfUnacceptedQuest()
        {
            quests.RecordEnemyDefeated("Giant Cellar Rat");
            List<string> ids = new List<string>();
            List<int> states = new List<int>();
            List<int> progress = new List<int>();

            quests.CaptureState(ids, states, progress);

            int index = ids.IndexOf("quest_rats");
            Assert.GreaterOrEqual(index, 0);
            Assert.AreEqual((int)QuestState.NotStarted, states[index]);
            Assert.AreEqual(1, progress[index]);
        }

        [Test]
        public void FindQuestForGiver_MatchesNameInsideNpcName()
        {
            Assert.AreSame(ringQuest, quests.FindQuestForGiver("Lady Othelia"));
            Assert.AreSame(scrapQuest, quests.FindQuestForGiver("Baldur"));
            Assert.IsNull(quests.FindQuestForGiver("Cursed Commander"));
        }

        #endregion

        #region Dialogue

        [Test]
        public void QuestDialogue_NotStarted_UsesGiversOwnIntro()
        {
            Assert.IsNull(QuestDialogueBuilder.BuildOpeningNode(ratQuest, QuestState.NotStarted, 0, "Barnaby", null, null));
        }

        [Test]
        public void QuestDialogue_InProgress_RemindsWithoutHandIn()
        {
            List<DialogueNodeSO> nodes = new List<DialogueNodeSO>();
            DialogueNodeSO node = QuestDialogueBuilder.BuildOpeningNode(ratQuest, QuestState.InProgress, 1, "Barnaby", null, nodes);
            TrackAll(nodes);

            StringAssert.Contains("Still rats down there.", node.DialogueText);
            StringAssert.Contains("1/3", node.DialogueText);
            for (int i = 0; i < node.Options.Count; i++)
            {
                StringAssert.DoesNotContain("ACTION_COMPLETE_QUEST", node.Options[i].CombatDebuffTag);
            }
        }

        [Test]
        public void QuestDialogue_Ready_OffersHandInLeadingToThanks()
        {
            List<DialogueNodeSO> nodes = new List<DialogueNodeSO>();
            DialogueNodeSO node = QuestDialogueBuilder.BuildOpeningNode(ratQuest, QuestState.InProgress, 3, "Barnaby", null, nodes);
            TrackAll(nodes);

            DialogueOption turnIn = node.Options[0];
            StringAssert.StartsWith(QuestDialogueBuilder.TurnInPrefix, turnIn.OptionText);
            Assert.AreEqual("[ACTION_COMPLETE_QUEST:quest_rats]", turnIn.CombatDebuffTag);
            Assert.IsNotNull(turnIn.NextNodeSuccess);
            Assert.AreEqual("Thank you!", turnIn.NextNodeSuccess.DialogueText);
        }

        [Test]
        public void QuestDialogue_KeepsSideOptionsBeforeLeave()
        {
            DialogueOption shop = new DialogueOption("[Blacksmith] Show me your wares.", null, false, 10, "", null, DialogueActionTrigger.TAG_OPEN_SHOP);
            List<DialogueNodeSO> nodes = new List<DialogueNodeSO>();
            DialogueNodeSO node = QuestDialogueBuilder.BuildOpeningNode(scrapQuest, QuestState.Completed, 5, "Baldur", new List<DialogueOption> { shop }, nodes);
            TrackAll(nodes);

            Assert.AreSame(shop, node.Options[0]);
            Assert.IsTrue(DialogueController.IsExitOption(node.Options[node.Options.Count - 1]));
        }

        [Test]
        public void TurnInChoice_CompletesQuestThroughDialogueActionTrigger()
        {
            quests.StartQuest("quest_rats");
            for (int i = 0; i < 3; i++) quests.RecordEnemyDefeated("Giant Cellar Rat");

            List<DialogueNodeSO> nodes = new List<DialogueNodeSO>();
            DialogueNodeSO node = QuestDialogueBuilder.BuildOpeningNode(ratQuest, QuestState.InProgress, 3, "Barnaby", null, nodes);
            TrackAll(nodes);

            DialogueActionTrigger trigger = Track(new GameObject("Test_Trigger")).AddComponent<DialogueActionTrigger>();
            MethodInfo handle = typeof(DialogueActionTrigger).GetMethod("HandleOptionSelected", BindingFlags.Instance | BindingFlags.NonPublic);
            handle.Invoke(trigger, new object[] { node.Options[0] });

            Assert.AreEqual(QuestState.Completed, quests.GetQuestState("quest_rats"));
        }

        [Test]
        public void ExtractQuestId_ReadsIdFromActionTags()
        {
            Assert.AreEqual("quest_swamp_herbs", DialogueActionTrigger.ExtractQuestId("[ACTION_ACCEPT_QUEST:quest_swamp_herbs]", "I can gather the blossoms.", "fallback"),
                "Mirabel's accept tag must start her own quest, not the fallback.");
            Assert.AreEqual("quest_cellar_pests", DialogueActionTrigger.ExtractQuestId("[ACTION_ACCEPT_QUEST:quest_cellar_pests:bonus]", "", "fallback"));
            Assert.AreEqual("quest_scrap_metal", DialogueActionTrigger.ExtractQuestId("[ACTION_COMPLETE_QUEST:quest_scrap_metal]", "", "fallback"));
            Assert.AreEqual("fallback", DialogueActionTrigger.ExtractQuestId("[ACTION_ACCEPT_QUEST]", "", "fallback"));
        }

        [Test]
        public void AcceptWithBonusTag_RecordsNegotiatedBonus()
        {
            DialogueActionTrigger trigger = Track(new GameObject("Test_Trigger")).AddComponent<DialogueActionTrigger>();
            MethodInfo handle = typeof(DialogueActionTrigger).GetMethod("HandleOptionSelected", BindingFlags.Instance | BindingFlags.NonPublic);
            handle.Invoke(trigger, new object[] { new DialogueOption("Deal.", null, false, 10, "", null, "[ACTION_ACCEPT_QUEST:quest_rats:bonus]") });

            Assert.AreEqual(QuestState.InProgress, quests.GetQuestState("quest_rats"));
            Assert.IsTrue(quests.HasNegotiatedBonus("quest_rats"));
        }

        #endregion

        #region Markers, Tracker & Journal

        [Test]
        public void Marker_ShowsExclamationThenQuestionMarks()
        {
            Assert.AreEqual(QuestMarkerKind.Available, QuestGiverMarker.ResolveMarker(QuestState.NotStarted, false));
            Assert.AreEqual(QuestMarkerKind.InProgress, QuestGiverMarker.ResolveMarker(QuestState.InProgress, false));
            Assert.AreEqual(QuestMarkerKind.ReadyToTurnIn, QuestGiverMarker.ResolveMarker(QuestState.InProgress, true));
            Assert.AreEqual(QuestMarkerKind.None, QuestGiverMarker.ResolveMarker(QuestState.Completed, true));
            Assert.AreEqual("!", QuestGiverMarker.SymbolFor(QuestMarkerKind.Available));
            Assert.AreEqual("?", QuestGiverMarker.SymbolFor(QuestMarkerKind.ReadyToTurnIn));
        }

        [Test]
        public void TrackerRow_ShowsLocationWhileInProgress_AndGiverWhenReady()
        {
            string inProgress = QuestEntryUI.FormatObjective(ratQuest, 1, 3, false);
            StringAssert.Contains("1/3", inProgress);
            StringAssert.Contains("Wine cellar", inProgress);

            string ready = QuestEntryUI.FormatObjective(ratQuest, 3, 3, false);
            StringAssert.Contains("Return to Barnaby", ready);
        }

        [Test]
        public void Tracker_DropsCompletedQuestsAfterTheirMoment()
        {
            GameObject hudGo = Track(new GameObject("Quest_Tracker_Card", typeof(RectTransform)));
            QuestHUDUIController hud = hudGo.AddComponent<QuestHUDUIController>();

            inventory.AddItem(ring, 1);
            quests.StartQuest("quest_ring");
            quests.StartQuest("quest_rats");
            quests.CompleteQuest("quest_ring");

            List<QuestSO> shown = new List<QuestSO>();
            hud.CollectDisplayedQuests(quests, shown);
            CollectionAssert.Contains(shown, ratQuest);
            CollectionAssert.DoesNotContain(shown, ringQuest, "An old completed quest belongs in the journal, not the tracker.");

            hud.MarkRecentlyCompleted("quest_ring");
            hud.CollectDisplayedQuests(quests, shown);
            CollectionAssert.Contains(shown, ringQuest, "A just-finished quest lingers briefly.");
            Assert.AreSame(ratQuest, shown[0], "Active quests are listed first.");
        }

        [Test]
        public void Journal_ListsActiveAndCompletedQuests()
        {
            inventory.AddItem(ring, 1);
            quests.StartQuest("quest_ring");
            quests.CompleteQuest("quest_ring");
            quests.StartQuest("quest_herbs");

            string text = QuestJournalUI.BuildJournalText(quests);

            StringAssert.Contains("ACTIVE", text);
            StringAssert.Contains("Herbs for the Healer", text);
            StringAssert.Contains("Given by Mirabel", text);
            StringAssert.Contains("COMPLETED", text);
            StringAssert.Contains("The Lost Signet Ring", text);
            Assert.Less(text.IndexOf("Herbs for the Healer"), text.IndexOf("The Lost Signet Ring"));
        }

        [Test]
        public void Journal_EmptyPointsToQuestGivers()
        {
            StringAssert.Contains("No quests yet", QuestJournalUI.BuildJournalText(quests));
        }

        [Test]
        public void Notification_RewardTextListsGoldAndItems()
        {
            Assert.AreEqual("<color=#F1C40F>+45 gold</color>, 2x Health Potion", QuestNotificationUI.FormatRewards(45, potion, 2));
            Assert.AreEqual("Health Potion", QuestNotificationUI.FormatRewards(0, potion, 1));
        }

        #endregion

        #region Quest Data

        [Test]
        public void QuestAssets_HaveGiversObjectivesAndSpecRewards()
        {
            QuestSO rats = LoadQuest("Quest_CellarPests");
            Assert.AreEqual("Barnaby", rats.QuestGiverName);
            Assert.AreEqual(QuestObjectiveType.DefeatEnemies, rats.ObjectiveType);
            Assert.IsTrue(rats.CountsEnemy("Giant Cellar Rat"));
            Assert.AreEqual(2, rats.RewardItemAmount, "Barnaby promises two healing draughts.");

            QuestSO ringAsset = LoadQuest("Quest_LostSignetRing");
            Assert.AreEqual(QuestObjectiveType.CollectItem, ringAsset.ObjectiveType);
            Assert.AreEqual("item_signet_ring", ringAsset.ObjectiveItem.ItemID);
            StringAssert.Contains("tower", ringAsset.ObjectiveLocation.ToLowerInvariant());

            QuestSO herbs = LoadQuest("Quest_SwampHerbs");
            Assert.AreEqual("item_swamp_herb", herbs.ObjectiveItem.ItemID);
            Assert.AreEqual("Forest Path", herbs.ObjectiveLocation);
            Assert.AreEqual("item_greater_potion", herbs.BonusRewardItem.ItemID, "The Nature check promises a Greater Health Potion instead of poison.");
            Assert.AreEqual(0, herbs.BonusRewardGold);

            QuestSO scrap = LoadQuest("Quest_ScrapMetal");
            Assert.AreEqual(QuestObjectiveType.ScrapMetal, scrap.ObjectiveType);
            Assert.AreEqual("Baldur", scrap.QuestGiverName);
        }

        [Test]
        public void SignetRing_IsOnlyInTheTreasureTower()
        {
            string ringGuid = AssetDatabase.AssetPathToGUID("Assets/Data/Item_SignetRing.asset");
            Assert.IsFalse(string.IsNullOrEmpty(ringGuid));

            string courtyard = System.IO.File.ReadAllText("Assets/Scenes/Zone_3_CastleCourtyard.unity");
            string tower = System.IO.File.ReadAllText("Assets/Scenes/Zone_6_Tower.unity");

            StringAssert.DoesNotContain($"itemReward: {{fileID: 11400000, guid: {ringGuid}", courtyard);
            StringAssert.Contains($"itemReward: {{fileID: 11400000, guid: {ringGuid}", tower);
        }

        #endregion

        #region Helpers

        private T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        private void TrackAll(List<DialogueNodeSO> nodes)
        {
            for (int i = 0; i < nodes.Count; i++) spawned.Add(nodes[i]);
        }

        private ItemSO CreateItem(string id, string itemName, ItemType type)
        {
            ItemSO item = Track(ScriptableObject.CreateInstance<ItemSO>());
            item.Initialize(id, itemName, "", type, 0, 0, 0, type == ItemType.Consumable);
            return item;
        }

        private QuestSO CreateQuest(string id, string title, int required, int gold, int bonusGold, ItemSO reward)
        {
            QuestSO quest = Track(ScriptableObject.CreateInstance<QuestSO>());
            quest.Initialize(id, title, title + " description", QuestState.NotStarted, required, gold, bonusGold, reward);
            return quest;
        }

        private static QuestSO LoadQuest(string fileName)
        {
            QuestSO quest = AssetDatabase.LoadAssetAtPath<QuestSO>($"Assets/Data/Quests/{fileName}.asset");
            Assert.IsNotNull(quest, $"Quest asset {fileName} missing.");
            return quest;
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
