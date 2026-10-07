using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Vili, 2026-10-07: Oakhaven got flower beds, verges and rocks (Village_Greenery) and the hero now starts in a
    /// forest glade (Spawn_Clearing) at the end of the arrival road. The hero must still walk the road from the
    /// glade into the village without bumping into a tree, rock or bush.
    /// </summary>
    public class VillageGreeneryPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;

        // The editor's PlayerPrefs are shared with the main project: New Adventure clears the save slot
        [SetUp]
        public void BackupSave() => saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;

        [TearDown]
        public void RestoreSave()
        {
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Hero_WalksFromTheGlade_IntoTheVillage()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            for (int i = 0; i < 10; i++) yield return null;
            MainMenuController menu = MainMenuController.Instance ?? Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
#if UNITY_EDITOR
            CharacterClassSO cls = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterClassSO>("Assets/Data/Character_Warrior_SirRoland.asset");
#else
            CharacterClassSO cls = null;
#endif
            Assert.IsTrue(menu != null && cls != null, "Main menu and the warrior class load.");
            menu.SelectCharacterClass(cls);
            float until = Time.realtimeSinceStartup + 30f;
            while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.realtimeSinceStartup < until) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
            StoryPanelUI.CloseIfOpen();

            Assert.IsNotNull(GameObject.Find("Spawn_Clearing"), "The glade around the spawn is in the scene.");
            Assert.IsNotNull(GameObject.Find("Village_Greenery"), "The village greenery is in the scene.");

            PlayerUnit hero = Object.FindAnyObjectByType<PlayerUnit>();
            CharacterController cc = hero != null ? hero.GetComponent<CharacterController>() : null;
            if (cc == null) Assert.Inconclusive("The hero has no CharacterController to walk with.");

            // Along the arrival road: past the village notice that stands on it (RuntimeLoreSpawner), the bend,
            // then onto the ring road
            Vector3[] waypoints = { new Vector3(-27.3f, 0f, -26.6f), new Vector3(-25.5f, 0f, -23.4f), new Vector3(-20f, 0f, -17f), new Vector3(-14.5f, 0f, -11.5f) };
            foreach (Vector3 wp in waypoints)
            {
                float stop = Time.realtimeSinceStartup + 12f;
                while (Time.realtimeSinceStartup < stop)
                {
                    Vector3 to = wp - hero.transform.position;
                    to.y = 0f;
                    if (to.magnitude < 0.6f) break;
                    StoryPanelUI.CloseIfOpen(); // a story page pauses the game
                    float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    cc.Move(to.normalized * 5f * dt + Vector3.down * 0.5f * dt);
                    yield return null;
                }
                Vector3 left = wp - hero.transform.position;
                left.y = 0f;
                string near = "";
                if (left.magnitude >= 1.2f)
                {
                    foreach (Collider col in Physics.OverlapSphere(hero.transform.position + Vector3.up, 1.6f))
                    {
                        if (col.transform.IsChildOf(hero.transform)) continue;
                        Transform t = col.transform;
                        string path = t.name;
                        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
                        near += path + (col.isTrigger ? " (trigger)" : "") + "; ";
                    }
                }
                Assert.Less(left.magnitude, 1.2f, $"The hero reached {wp} on the road (stuck at {hero.transform.position} next to {near}).");
            }
        }
    }
}
