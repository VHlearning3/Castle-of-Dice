using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Vili, 2026-10-07: the level-up cards ignored clicks (the controller object sat inactive in Zone 1, so the
    /// cards were never wired), the pause menu's slider names hid behind the sliders, and the cellar became a
    /// wooden wine cellar. Each test starts a New Adventure in Oakhaven.
    /// </summary>
    public class PauseLevelUpCellarPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;
        private float? musicBackup;
        private float? sfxBackup;

        // The editor's PlayerPrefs are shared with the main project: New Adventure clears the save slot
        [SetUp]
        public void BackupPrefs()
        {
            saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
            musicBackup = PlayerPrefs.HasKey(PauseMenuUI.MusicVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.MusicVolumeKey) : (float?)null;
            sfxBackup = PlayerPrefs.HasKey(PauseMenuUI.SfxVolumeKey) ? PlayerPrefs.GetFloat(PauseMenuUI.SfxVolumeKey) : (float?)null;
        }

        [TearDown]
        public void RestorePrefs()
        {
            Time.timeScale = 1f;
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            if (musicBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.MusicVolumeKey, musicBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.MusicVolumeKey);
            if (sfxBackup.HasValue) PlayerPrefs.SetFloat(PauseMenuUI.SfxVolumeKey, sfxBackup.Value);
            else PlayerPrefs.DeleteKey(PauseMenuUI.SfxVolumeKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator LevelUpCard_TakesTheClick()
        {
            yield return StartAdventure();
            PlayerUnit hero = Object.FindAnyObjectByType<PlayerUnit>();

            PlayerProgressionManager.Instance.GrantBonusLevel("Level-up click test");
            yield return null;
            LevelUpUIController levelUp = LevelUpUIController.Instance;
            Assert.IsNotNull(levelUp, "The level-up screen registers itself when it opens, even from an inactive object.");
            Assert.IsTrue(levelUp.IsModalOpen, "The level-up screen opened.");
            int maxHpBefore = hero.MaxHP; // the level's own hit points are already in

            GameObject card = GameObject.Find("Card_Resilience");
            Assert.IsNotNull(card, "Hero's Resilience card exists.");
            RectTransform rect = (RectTransform)card.transform;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            EventSystem events = EventSystem.current;
            Assert.IsNotNull(events, "Oakhaven has an EventSystem.");
            var hits = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = screenPoint }, hits);
            Assert.IsTrue(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(card.transform),
                $"Nothing covers the card (top hit: {(hits.Count > 0 ? hits[0].gameObject.name : "none")}).");

            ExecuteEvents.Execute(card, new PointerEventData(events) { position = screenPoint, button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.IsFalse(levelUp.IsModalOpen, "Choosing a card closes the level-up screen.");
            Assert.AreEqual(maxHpBefore + 5, hero.MaxHP, "Hero's Resilience adds 5 max HP.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator PauseMenu_SlidersShowNameAndValue()
        {
            yield return StartAdventure();
            PauseMenuUI.Instance.Open();
            yield return null;
            Assert.IsTrue(PauseMenuUI.IsPaused);

            foreach (string name in new[] { "Music", "Sound Effects" })
            {
                TextMeshProUGUI label = FindUi<TextMeshProUGUI>(name + "_Label");
                TextMeshProUGUI value = FindUi<TextMeshProUGUI>(name + "_Value");
                Slider slider = FindUi<Slider>(name + "_Slider");
                Assert.AreEqual(name, label.text);
                Assert.Greater(Bottom((RectTransform)label.transform), Top((RectTransform)slider.transform) - 0.01f,
                    $"The {name} name sits above its slider, not behind it.");

                slider.value = 0.42f;
                Assert.AreEqual("42%", value.text, $"The {name} percentage follows the slider.");
            }

            PauseMenuUI.Instance.Resume();
            Assert.IsFalse(PauseMenuUI.IsPaused);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Cellar_WoodenDressing_FightStillStarts()
        {
            yield return StartAdventure();
            Transform cellar = GameObject.Find("Cellar_Chamber").transform;
            Assert.IsNotNull(cellar.Find("Cellar_Geometry/Cellar_Dressing/Stores/Barrel_Rack_NE"), "The wine rack is in the cellar.");
            Assert.AreEqual(0, cellar.Find("CombatGrid/Tiles").childCount, "No grid preview lies on the floor before the fight.");

            // Down the hatch: the teleporter ignores the first moments after a scene load
            float settle = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            PlayerUnit hero = Object.FindAnyObjectByType<PlayerUnit>();
            GameObject.Find("Village_Cellar_Hatch").GetComponent<DoorTeleporter>().Interact(hero);
            float until = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < until && (TurnManager.Instance == null || !TurnManager.Instance.IsCombatActive)) yield return null;
            Assert.IsTrue(TurnManager.Instance != null && TurnManager.Instance.IsCombatActive, "The rats attack once the hero is down the ladder.");

            int walkable = 0;
            foreach (GridTile tile in Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None))
            {
                if (tile.IsWalkable) walkable++;
            }
            Assert.GreaterOrEqual(walkable, 60, "Barrels and crates leave most of the cellar floor to fight on.");
            foreach (CombatUnit unit in TurnManager.Instance.ActiveUnits)
            {
                GridTile under = GridManager.Instance.GetTileAt(unit.GridPosition);
                Assert.IsTrue(under != null && under.IsWalkable, $"{unit.name} stands on open floor.");
            }
        }

        private static IEnumerator StartAdventure()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            for (int i = 0; i < 10; i++) yield return null;
            MainMenuController menu = MainMenuController.Instance ?? Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            CharacterClassSO cls = LoadClass("Character_Warrior_SirRoland");
            Assert.IsTrue(menu != null && cls != null, "Main menu and the warrior class load.");
            menu.SelectCharacterClass(cls);
            for (int i = 0; i < 5; i++) yield return null;
            float until = Time.realtimeSinceStartup + 30f;
            while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.realtimeSinceStartup < until) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
            StoryPanelUI.CloseIfOpen();
            yield return null;
        }

        private static T FindUi<T>(string objectName) where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (c.gameObject.name == objectName) return c;
            }
            Assert.Fail($"{objectName} not found.");
            return null;
        }

        private static float Bottom(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[0].y;
        }

        private static float Top(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[1].y;
        }

        private static CharacterClassSO LoadClass(string assetName)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterClassSO>($"Assets/Data/{assetName}.asset");
#else
            return null;
#endif
        }
    }
}
