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
    /// Vili, 2026-10-07: the Forest Path's exits to Oakhaven and the Courtyard are tall arches on two pillars that
    /// the hero walks under, with a road going on into the woods beyond (Gate_Outskirts), and the lockpick gate to
    /// the Hidden Trail opens onto a path into the forest. The hero must walk under each arch into its door, and the
    /// exit signs keep a brazier on each end of the arch.
    /// </summary>
    public class ForestPathGatesPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;
        private PlayerUnit hero;
        private CharacterController cc;

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
        public IEnumerator Hero_WalksUnderTheVillageArch() => WalkUnderArch("Door_Forest_Village", new Vector3(0f, 0.1f, -86f), Vector3.back);

        [UnityTest, Timeout(300000)]
        public IEnumerator Hero_WalksUnderTheCourtyardArch() => WalkUnderArch("Door_Forest_Courtyard", new Vector3(0f, 0.1f, 46f), Vector3.forward);

        [UnityTest, Timeout(300000)]
        public IEnumerator HiddenTrailGate_OpensOntoAForestPath()
        {
            yield return EnterForestPath();
            Transform outskirts = GameObject.Find("Gate_Outskirts").transform;
            Assert.IsNotNull(outskirts.Find("West_Hidden_Trail_Path/Road"), "A path runs on behind the lockpick gate.");
            // The zone's west wall leaves an opening behind the gate's archway
            foreach (Collider c in Physics.OverlapBox(new Vector3(-40f, 3f, 15f), new Vector3(1.2f, 2f, 3f)))
            {
                Assert.IsFalse(c.name.StartsWith("Forest_Wall_West"), $"{c.name} still closes the wall behind the gate.");
            }
            Transform road = outskirts.Find("West_Hidden_Trail_Path/Road");
            Assert.Less(road.GetComponent<Renderer>().bounds.max.x, -36f, "The path starts at the gate.");
            Assert.Less(road.GetComponent<Renderer>().bounds.min.x, -60f, "The path leads well into the forest.");
        }

        private IEnumerator WalkUnderArch(string doorName, Vector3 start, Vector3 dir)
        {
            yield return EnterForestPath();
            Transform door = GameObject.Find(doorName).transform;
            Transform arch = door.Find("Arch_Model");
            Bounds archBounds = Bounds(arch.gameObject);
            Assert.Greater(archBounds.min.y, 4.5f, "The arch is high enough to walk under.");
            Assert.IsNotNull(door.Find("Gate_Pillar_L"), "The arch stands on a pillar on each side.");
            Assert.IsNotNull(door.Find("Gate_Pillar_R"), "The arch stands on a pillar on each side.");

            ZoneExitSign sign = door.GetComponentInChildren<ZoneExitSign>();
            Assert.IsNotNull(sign, "The exit has its sign.");
            Assert.AreEqual(2, sign.BrazierCount, "A brazier on each end of the arch.");
            Assert.Greater(sign.transform.position.y, archBounds.max.y, "The sign hangs above the arch.");

            cc.enabled = false;
            hero.transform.position = start;
            cc.enabled = true;
            yield return null;

            bool loading = false;
            Vector3 at = hero.transform.position;
            float stop = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < stop)
            {
                if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) { loading = true; break; }
                at = hero.transform.position;
                StoryPanelUI.CloseIfOpen();
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                cc.Move(dir * 5f * dt + Vector3.down * 0.5f * dt);
                yield return null;
            }
            Assert.IsTrue(loading, $"Walking under the arch enters {doorName} (hero stopped at {at}).");
            Assert.That(at.z, Is.InRange(archBounds.min.z - 1.6f, archBounds.max.z + 1.6f), $"The door opens under the arch (hero at {at}).");
        }

        private IEnumerator EnterForestPath()
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
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();

            SceneLoader.Instance.LoadScene("Zone_2_ForestPath");
            for (int i = 0; i < 2; i++) yield return null;
            yield return WaitForLoad();
            Assert.AreEqual("Zone_2_ForestPath", SceneManager.GetActiveScene().name);
            StoryPanelUI.CloseIfOpen();

            // Fights would freeze exploration; these tests are about the gates
            foreach (DungeonRoomController room in Object.FindObjectsByType<DungeonRoomController>(FindObjectsSortMode.None)) room.gameObject.SetActive(false);

            hero = Object.FindAnyObjectByType<PlayerUnit>();
            cc = hero != null ? hero.GetComponent<CharacterController>() : null;
            if (cc == null) Assert.Inconclusive("The hero has no CharacterController to walk with.");

            // Doors ignore the first moments after a scene load
            float settle = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < settle) yield return null;
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
