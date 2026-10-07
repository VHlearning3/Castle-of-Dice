using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Vili, 2026-10-07: the Hidden Trail's gate back to the Forest Path is a tall arch on two pillars like the
    /// Forest Path's gates, with a road going on into the woods behind it (Gate_Outskirts). The hero walks under
    /// the arch into the door, and the sign and braziers sit above the arch.
    /// </summary>
    public class HiddenTrailGatePlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;

        // The editor's PlayerPrefs are shared with the main project: New Adventure clears the save slot
        [SetUp]
        public void BackupSave() => saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;

        [TearDown]
        public void RestoreSave()
        {
            Time.timeScale = 1f;
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Hero_WalksUnderTheTrailArch_IntoTheForestDoor()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            for (int i = 0; i < 10; i++) yield return null;
            MainMenuController menu = MainMenuController.Instance ?? Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
#if UNITY_EDITOR
            CharacterClassSO cls = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterClassSO>("Assets/Data/Character_Rogue_Corvo.asset");
#else
            CharacterClassSO cls = null;
#endif
            Assert.IsTrue(menu != null && cls != null, "Main menu and the rogue class load.");
            menu.SelectCharacterClass(cls);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();

            SceneLoader.Instance.LoadScene("Zone_2.5_SecretForestPath");
            for (int i = 0; i < 2; i++) yield return null;
            yield return WaitForLoad();
            Assert.AreEqual("Zone_2.5_SecretForestPath", SceneManager.GetActiveScene().name);
            StoryPanelUI.CloseIfOpen();

            Transform door = GameObject.Find("Hidden_Trail_Layout").transform.Find("Door_Trail_Forest");
            Transform arch = door.Find("Arch_Model");
            Bounds archBounds = Bounds(arch.gameObject);
            Assert.Greater(archBounds.min.y, 4.5f, "The arch is high enough to walk under.");
            Assert.IsNotNull(door.Find("Gate_Pillar_L"), "The arch stands on a pillar on each side.");
            Assert.IsNotNull(door.Find("Gate_Pillar_R"), "The arch stands on a pillar on each side.");
            Assert.IsNotNull(GameObject.Find("Gate_Outskirts"), "A road and forest lie behind the gate.");

            ZoneExitSign sign = door.GetComponentInChildren<ZoneExitSign>();
            Assert.IsNotNull(sign, "The gate has its sign.");
            Assert.AreEqual(2, sign.BrazierCount, "A brazier on each end of the arch.");
            Assert.Greater(sign.transform.position.y, archBounds.max.y, "The sign hangs above the arch.");

            PlayerUnit hero = Object.FindAnyObjectByType<PlayerUnit>();
            CharacterController cc = hero != null ? hero.GetComponent<CharacterController>() : null;
            if (cc == null) Assert.Inconclusive("The hero has no CharacterController to walk with.");
            cc.enabled = false;
            hero.transform.position = new Vector3(0f, 0.1f, -20f);
            cc.enabled = true;
            float settle = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < settle) yield return null;

            bool loading = false;
            Vector3 at = hero.transform.position;
            float stop = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < stop)
            {
                if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) { loading = true; break; }
                at = hero.transform.position;
                StoryPanelUI.CloseIfOpen();
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                cc.Move(Vector3.back * 5f * dt + Vector3.down * 0.5f * dt);
                yield return null;
            }
            Assert.IsTrue(loading, $"Walking south under the arch enters the Forest Path door (hero stopped at {at}).");
            Assert.That(at.z, Is.InRange(archBounds.min.z - 1.6f, archBounds.max.z + 1.6f), $"The door opens under the arch (hero at {at}).");
        }

        private static IEnumerator WaitForLoad()
        {
            float until = Time.realtimeSinceStartup + 30f;
            while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.realtimeSinceStartup < until) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
        }

        private static Bounds Bounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
