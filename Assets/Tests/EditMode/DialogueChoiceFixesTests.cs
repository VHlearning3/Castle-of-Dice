#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Vili, 2026-10-07: chosen dialogue choices came back (the Commander's checks after his answer, Baldur's
    /// [Lore] after [Back]), long choices were cut off, and the Library's side door let every hero onto the
    /// Rogue's Hidden Trail.
    /// </summary>
    [TestFixture]
    public class DialogueChoiceFixesTests
    {
        private readonly List<Object> spawned = new List<Object>();
        private PlayerDataSO previousSession;

        [SetUp]
        public void SetUp()
        {
            previousSession = PlayerDataSO.Session;
            PlayerDataSO.Session = Track(ScriptableObject.CreateInstance<PlayerDataSO>());
        }

        [TearDown]
        public void TearDown()
        {
            PlayerDataSO.Session = previousSession;
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
        }

        #region Chosen choices

        [Test]
        public void ChosenQuestionsAndChecks_AreNotOfferedAgain_ButNavigationShopsAndExitsAre()
        {
            DialogueOption lore = new DialogueOption("[Lore] What do you know about the castle defences?", null);
            DialogueOption check = new DialogueOption("[Persuasion DC 13] Pay me more.", null, true, 13, "Persuasion Check", null);
            DialogueOption shop = new DialogueOption("[Blacksmith] Show me your wares.", null, false, 10, "", null, DialogueActionTrigger.TAG_OPEN_SHOP);
            DialogueOption back = new DialogueOption("[Back] Let me ask about something else.", null);
            DialogueOption buy = new DialogueOption("[Buy] Scroll of Embers - 50 gold", null, false, 10, "", null, "[ACTION_BUY:scroll_embers:50]");
            DialogueOption exit = new DialogueOption("[Exit] Farewell.", null, false, 10, "", null, DialogueActionTrigger.TAG_CLOSE_DIALOGUE);
            DialogueNodeSO hub = CreateNode(shop, lore, check, buy, back, exit);
            DialogueOption fight = VillageNPC.CreateFightOption();

            List<DialogueOption> chosen = new List<DialogueOption> { lore, check, shop, back, buy, exit, fight };
            List<DialogueOption> shown = DialogueController.ComposeOptions(hub, new List<DialogueOption> { fight }, chosen);

            CollectionAssert.AreEqual(new[] { shop, buy, back, fight, exit }, shown);
        }

        [Test]
        public void NothingChosen_ShowsEveryChoice()
        {
            DialogueOption lore = new DialogueOption("[Lore] Tell me more.", null);
            DialogueOption exit = new DialogueOption("[Exit] Farewell.", null, false, 10, "", null, DialogueActionTrigger.TAG_CLOSE_DIALOGUE);
            DialogueNodeSO hub = CreateNode(lore, exit);

            CollectionAssert.AreEqual(new[] { lore, exit }, DialogueController.ComposeOptions(hub, null, new List<DialogueOption>()));
        }

        [Test]
        public void Baldur_LoreIsGoneAfterBack_ButTheShopStays()
        {
            DialogueController dialogue = Track(new GameObject("T_Dialogue")).AddComponent<DialogueController>();
            DialogueNodeSO start = VillageNPC.BuildBaldurDialogueTree();
            TrackTree(start);
            DialogueOption lore = FindOption(start, "[Lore]");
            DialogueOption quests = FindOption(start, "[Quests]");
            DialogueNodeSO questsNode = quests.NextNodeSuccess;

            dialogue.StartDialogue(start, null);
            try
            {
                dialogue.SelectOption(quests);
                Assert.AreSame(questsNode, dialogue.CurrentNode);
                dialogue.SelectOption(FindOption(questsNode, "[Back]"));
                Assert.AreSame(start, dialogue.CurrentNode);

                List<DialogueOption> shown = dialogue.GetVisibleOptions(start);
                Assert.IsFalse(shown.Contains(quests), "[Quests] was already asked.");
                Assert.IsTrue(shown.Contains(lore));
                Assert.IsTrue(shown.Contains(FindOption(start, "[Blacksmith]")), "The shop is always on offer.");
                Assert.IsTrue(shown.Contains(FindOption(start, "[Exit]")));
            }
            finally
            {
                dialogue.EndDialogue();
            }

            dialogue.StartDialogue(start, null);
            Assert.IsTrue(dialogue.GetVisibleOptions(start).Contains(quests), "A new conversation offers everything again.");
            dialogue.EndDialogue();
        }

        [Test]
        public void CommanderChoices_AreOnTheOpeningLineOnly()
        {
            DialogueNodeSO intro = AssetDatabase.LoadAssetAtPath<DialogueNodeSO>("Assets/Data/Dialogues/Commander_Intro.asset");
            PlayerUnit knight = CreateHero("Character_Warrior_SirRoland");

            DialogueNodeSO opening = BossChoices.BuildIntro(BossChoices.CommanderBossId, intro, knight);
            TrackTree(opening);

            Assert.AreNotSame(intro, opening, "The asset itself is never changed.");
            Assert.AreEqual(intro.DialogueText, opening.DialogueText);
            DialogueOption duel = FindOption(opening, "[Warrior]");
            Assert.IsNotNull(FindOption(opening, "[Soldier's Honor]"));
            Assert.IsNotNull(FindOption(opening, "[Fight]"), "The intro's own choice stays.");

            DialogueNodeSO accepted = duel.NextNodeSuccess;
            Assert.AreEqual(1, accepted.Options.Count);
            StringAssert.StartsWith("[Continue]", accepted.Options[0].OptionText);
        }

        #endregion

        #region Choice wrapping

        [Test]
        public void LongChoice_WrapsAndItsButtonGrows_ShortChoiceKeepsOneLine()
        {
            Button shortButton = CreateOptionButton("[Exit] Farewell.");
            Button longButton = CreateOptionButton("[Honor / Persuasion Check | DC 13 (Need 12+)] [Warrior] Then face me alone, soldier to soldier. " +
                "Send your guard away, and let the steel decide what the curse could not, here before the gate you kept so long.");

            float shortHeight = DialogueUIController.FitOptionButtonHeight(shortButton.gameObject, shortButton.GetComponentInChildren<TMP_Text>(), 840f);
            float longHeight = DialogueUIController.FitOptionButtonHeight(longButton.gameObject, longButton.GetComponentInChildren<TMP_Text>(), 840f);

            Assert.AreEqual(DialogueUIController.MinOptionButtonHeight, shortHeight, "One line keeps the usual height.");
            Assert.Greater(longHeight, DialogueUIController.MinOptionButtonHeight + 10f, "Two or more lines make the button taller.");
            Assert.AreEqual(longHeight, longButton.GetComponent<LayoutElement>().preferredHeight);
        }

        #endregion

        #region Library side door

        [Test]
        public void HiddenTrailDoors_OpenOnlyForTheRogue()
        {
            PlayerUnit knight = CreateHero("Character_Warrior_SirRoland");
            PlayerUnit mage = CreateHero("Character_Mage_Elira");
            PlayerUnit rogue = CreateHero("Character_Rogue_Corvo");

            Assert.IsFalse(DoorTeleporter.CanUseDoor(SecretForestPath.SceneName, knight));
            Assert.IsFalse(DoorTeleporter.CanUseDoor(SecretForestPath.SceneName, mage));
            Assert.IsTrue(DoorTeleporter.CanUseDoor(SecretForestPath.SceneName, rogue), "The rogue uses her trail.");
            Assert.IsTrue(DoorTeleporter.CanUseDoor("Zone_4_Library", knight), "Every other door stays open, the trail's way out too.");
            Assert.IsTrue(DoorTeleporter.CanUseDoor("Zone_2_ForestPath", mage));
        }

        #endregion

        #region Helpers

        private PlayerUnit CreateHero(string classAsset)
        {
            PlayerDataSO.Session.SelectedClass = AssetDatabase.LoadAssetAtPath<CharacterClassSO>($"Assets/Data/{classAsset}.asset");
            GameObject go = Track(new GameObject("Hero_" + classAsset));
            go.AddComponent<StatusEffectController>();
            PlayerUnit hero = go.AddComponent<PlayerUnit>();
            hero.InitializeUnit();
            Assert.IsNotNull(hero.CharacterClass, classAsset);
            return hero;
        }

        private Button CreateOptionButton(string text)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/UI/OptionButtonPrefab.prefab");
            GameObject go = Track((GameObject)Object.Instantiate(prefab));
            TMP_Text label = go.GetComponentInChildren<TMP_Text>();
            label.margin = new Vector4(20f, 0f, 20f, 0f);
            label.fontSize = 15f;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.text = text;
            return go.GetComponent<Button>();
        }

        private DialogueNodeSO CreateNode(params DialogueOption[] options)
        {
            DialogueNodeSO node = Track(ScriptableObject.CreateInstance<DialogueNodeSO>());
            node.Initialize("Tester", "Hello.");
            node.SetOptions(new List<DialogueOption>(options));
            return node;
        }

        private void TrackTree(DialogueNodeSO node)
        {
            if (node == null || spawned.Contains(node) || AssetDatabase.Contains(node)) return;
            Track(node);
            foreach (DialogueOption option in node.Options)
            {
                TrackTree(option.NextNodeSuccess);
                TrackTree(option.NextNodeFailure);
            }
        }

        private static DialogueOption FindOption(DialogueNodeSO node, string prefix)
        {
            foreach (DialogueOption option in node.Options)
            {
                if (option.OptionText.StartsWith(prefix)) return option;
            }
            Assert.Fail($"'{node.name}' has no {prefix} choice.");
            return null;
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
