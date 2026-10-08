using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// The secret boss quest: the tavern's blonde elf warns of her twin (no quest shows), the Tower's great mirror
    /// laughs and the twin steps out of it, says her piece and fights; beating her shows the quest, completed, and
    /// the mirror stays quiet afterwards. With TWIN_SHOTS_DIR set, saves review screenshots.
    /// </summary>
    public class MirrorTwinPlayTests
    {
        private static readonly string[] SavedKeys = { "CastleOfDice_SaveData", "CastleOfDice_MusicVolume", "CastleOfDice_SfxVolume" };
        private readonly string[] backup = new string[3];
        private readonly bool[] had = new bool[3];

        [SetUp]
        public void BackupSave()
        {
            for (int i = 0; i < SavedKeys.Length; i++)
            {
                had[i] = PlayerPrefs.HasKey(SavedKeys[i]);
                if (!had[i]) continue;
                backup[i] = i == 0 ? PlayerPrefs.GetString(SavedKeys[i]) : PlayerPrefs.GetFloat(SavedKeys[i]).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        [TearDown]
        public void RestoreSave()
        {
            DevTools.EncounterDebugOverlay.Visible = Application.isEditor;
            Time.timeScale = 1f;
            for (int i = 0; i < SavedKeys.Length; i++)
            {
                if (!had[i]) PlayerPrefs.DeleteKey(SavedKeys[i]);
                else if (i == 0) PlayerPrefs.SetString(SavedKeys[i], backup[i]);
                else PlayerPrefs.SetFloat(SavedKeys[i], float.Parse(backup[i], System.Globalization.CultureInfo.InvariantCulture));
            }
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TavernWarning_ThenTheTwinStepsOutOfTheMirrorAndFights()
        {
            LogAssert.ignoreFailingMessages = true;
            string shots = Environment.GetEnvironmentVariable("TWIN_SHOTS_DIR");
            bool takeShots = !string.IsNullOrEmpty(shots);
            DevTools.EncounterDebugOverlay.Visible = false; // the editor's F3 overlay would clutter the shots
            if (takeShots) Directory.CreateDirectory(shots);

            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            yield return Frames(10);
            MainMenuController menu = MainMenuController.Instance ?? UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "main menu");
#if UNITY_EDITOR
            CharacterClassSO cls = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterClassSO>("Assets/Data/Character_Warrior_SirRoland.asset");
#else
            CharacterClassSO cls = null;
#endif
            menu.SelectCharacterClass(cls);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();

            QuestManager quests = QuestManager.Instance;
            Assert.IsNotNull(quests.GetQuest(MirrorTwinQuest.QuestId), "the secret quest is registered");
            Assert.AreEqual(QuestState.NotStarted, quests.GetQuestState(MirrorTwinQuest.QuestId));

            // The tavern: the blonde elf's warning, and still nothing in the tracker
            SceneLoader.Instance.LoadScene(TavernEntrance.SceneName, TavernEntrance.SpawnInTavern);
            yield return Frames(2);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            GameObject elf = GameObject.Find(TavernElfWarning.ElfObjectName);
            Assert.IsNotNull(elf, "the blonde elf is in the tavern");
            TavernElfWarning warning = elf.GetComponent<TavernElfWarning>();
            Assert.IsNotNull(warning, "she can be talked to");
            Assert.IsNotNull(elf.GetComponent<Collider>(), "a click on her lands");
            warning.Interact(Hero());
            yield return Frames(3);
            DialogueController dialogue = DialogueController.Instance;
            Assert.IsTrue(dialogue.IsInDialogue);
            Assert.AreEqual(MirrorTwinQuest.WarningLine, dialogue.CurrentNode.DialogueText);
            if (takeShots) yield return Shot(Path.Combine(shots, "tavern_elf.png"), elf.transform.position + elf.transform.forward * 2.6f + Vector3.up * 2.3f, elf.transform.position + Vector3.up * 0.9f);
            dialogue.EndDialogue();
            yield return Frames(2);
            Assert.IsTrue(StoryFlags.Has(MirrorTwinQuest.WarnedFlag));
            Assert.AreEqual(QuestState.NotStarted, quests.GetQuestState(MirrorTwinQuest.QuestId), "the warning starts no quest in the tracker");

            // The Tower with its challenge done: the mirror room is open
            StoryFlags.Set(TowerChallenge.RunesSolvedFlag);
            StoryFlags.Set(TowerChallenge.GuardianDefeatedFlag);
            StoryFlags.Set(TowerChallenge.CompletedFlag);
            SceneLoader.Instance.LoadScene("Zone_6_Tower", "Spawn_From_Hall");
            yield return Frames(2);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            MirrorTwinEncounter encounter = UnityEngine.Object.FindAnyObjectByType<MirrorTwinEncounter>();
            Assert.IsNotNull(encounter, "the mirror's encounter is in the open room");
            MirrorTwinBoss twin = encounter.Twin;
            Assert.IsFalse(twin.gameObject.activeSelf, "the twin hides until the mirror wakes");
            Animator twinAnim = twin.UnitAnimator;

            // Walking up to the mirror sets off the cutscene
            Teleport(new Vector3(0f, Hero().transform.position.y, 25f), Quaternion.identity);
            float until = Time.realtimeSinceStartup + 5f;
            while (!encounter.IsPlaying && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(encounter.IsPlaying, "the mirror laughs when the hero comes near");
            Assert.AreEqual(GamePlayMode.Dialogue, GameManager.Instance.CurrentMode, "the hero stands still for it");
            yield return new WaitForSeconds(1.2f);
            if (takeShots) yield return Shot(Path.Combine(shots, "mirror_ripple.png"));
            yield return new WaitForSeconds(0.9f);
            if (twinAnim != null) twinAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(twin.gameObject.activeSelf, "she steps out of the glass");
            if (takeShots) yield return Shot(Path.Combine(shots, "twin_emerges.png"));
            yield return new WaitForSeconds(1.4f);
            if (takeShots) yield return Shot(Path.Combine(shots, "twin_steps_down.png"));
            yield return new WaitForSeconds(1.0f);
            if (takeShots)
            {
                yield return Shot(Path.Combine(shots, "twin_laughs.png"));
                Vector3 face = twin.transform.position + twin.transform.forward * 3.6f + Vector3.up * 1.4f;
                yield return Shot(Path.Combine(shots, "twin_closeup.png"), face, twin.transform.position + Vector3.up * 1.1f);
            }

            until = Time.realtimeSinceStartup + 8f;
            while ((DialogueController.Instance == null || !DialogueController.Instance.IsInDialogue) && Time.realtimeSinceStartup < until) yield return null;
            dialogue = DialogueController.Instance;
            Assert.IsTrue(dialogue.IsInDialogue, "she says her piece");
            Assert.AreEqual(MirrorTwinBoss.DisplayName, dialogue.CurrentNode.SpeakerName);
            StringAssert.Contains("sister warned you", dialogue.CurrentNode.DialogueText, "she knows her sister warned the hero");
            dialogue.EndDialogue();
            yield return Frames(10);
            Assert.IsTrue(TurnManager.Instance != null && TurnManager.Instance.IsCombatActive, "the fight starts");
            Assert.Contains(twin, (System.Collections.ICollection)TurnManager.Instance.ActiveUnits, "against the twin");
            Assert.AreEqual(58, twin.MaxHP);
            if (takeShots)
            {
                yield return new WaitForSeconds(1f);
                yield return Shot(Path.Combine(shots, "twin_fight.png"));
            }

            // Let her take a turn: a trident thrust or chain lightning, without errors
            TurnManager tm = TurnManager.Instance;
            until = Time.realtimeSinceStartup + 6f;
            while (tm.CurrentActiveUnit != twin && tm.CurrentActiveUnit != Hero() && Time.realtimeSinceStartup < until) yield return null;
            if (tm.CurrentActiveUnit == Hero()) tm.EndPlayerTurn();
            until = Time.realtimeSinceStartup + 6f;
            while (tm.CurrentActiveUnit != twin && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(twin, tm.CurrentActiveUnit, "her turn comes");
            yield return new WaitForSeconds(1.35f);
            if (takeShots) yield return Shot(Path.Combine(shots, "twin_turn.png"));
            until = Time.realtimeSinceStartup + 12f;
            while (tm.IsCombatActive && tm.CurrentActiveUnit == twin && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreNotEqual(twin, tm.CurrentActiveUnit, "her turn ends");

            // Beat her: the quest shows, completed, and pays
            int gold = InventoryManager.Instance.CurrentGold;
            twin.TakeDamage(999);
            until = Time.realtimeSinceStartup + 10f;
            while (TurnManager.Instance.IsCombatActive && Time.realtimeSinceStartup < until) yield return null;
            yield return Frames(10);
            Assert.IsFalse(TurnManager.Instance.IsCombatActive, "the fight is won");
            Assert.IsTrue(MirrorTwinQuest.IsDefeated, "her defeat is remembered");
            Assert.AreEqual(QuestState.Completed, quests.GetQuestState(MirrorTwinQuest.QuestId), "the quest appears, completed");
            Assert.GreaterOrEqual(InventoryManager.Instance.CurrentGold - gold, MirrorTwinQuest.RewardGold, "the reward is paid");

            // Back again: the mirror stays quiet
            SceneLoader.Instance.LoadScene("Zone_6_Tower", "Spawn_From_Hall");
            yield return Frames(2);
            yield return WaitForLoad();
            encounter = UnityEngine.Object.FindAnyObjectByType<MirrorTwinEncounter>();
            Teleport(new Vector3(0f, Hero().transform.position.y, 25f), Quaternion.identity);
            yield return new WaitForSeconds(1.5f);
            Assert.IsFalse(encounter.IsPlaying, "no second entrance");
            Assert.IsFalse(encounter.Twin.gameObject.activeSelf);
        }

        private static PlayerUnit Hero() => UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

        private static void Teleport(Vector3 position, Quaternion rotation)
        {
            PlayerExplorationMovement move = Hero().GetComponent<PlayerExplorationMovement>();
            if (move != null) move.TeleportTo(position, rotation);
            else Hero().transform.position = position;
        }

        private static IEnumerator Shot(string file, Vector3? eye = null, Vector3? target = null)
        {
            yield return null;
            Camera cam = Camera.main;
            if (cam == null) yield break;
            Vector3 oldPos = cam.transform.position;
            Quaternion oldRot = cam.transform.rotation;
            if (eye.HasValue && target.HasValue) cam.transform.SetPositionAndRotation(eye.Value, Quaternion.LookRotation(target.Value - eye.Value));
            foreach (PlanarMirror m in UnityEngine.Object.FindObjectsByType<PlanarMirror>(FindObjectsSortMode.None))
            {
                if (m.MirrorCamera != null && m.MirrorCamera.enabled) m.MirrorCamera.Render();
            }
            const int w = 1600, h = 900;
            RenderTexture rt = new RenderTexture(w, h, 24);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = prev;
            RenderTexture.active = null;
            cam.transform.SetPositionAndRotation(oldPos, oldRot);
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);
        }

        private static IEnumerator WaitForLoad()
        {
            float until = Time.realtimeSinceStartup + 30f;
            while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.realtimeSinceStartup < until) yield return null;
            yield return Frames(10);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }
    }
}
