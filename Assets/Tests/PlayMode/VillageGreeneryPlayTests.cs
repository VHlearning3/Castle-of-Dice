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
    /// Vili, 2026-10-07: Oakhaven got flower beds, verges and rocks (Village_Greenery), the hero now starts in a
    /// forest glade (Spawn_Clearing) at the end of the arrival road, and the forest gate became a tall arch on two
    /// pillars that the hero walks under, with the road going on up into the woods. The hero must still walk the
    /// road from the glade into the village, and walk under the arch into the forest door's trigger.
    /// </summary>
    public class VillageGreeneryPlayTests
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
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Hero_WalksFromTheGlade_IntoTheVillage()
        {
            yield return StartAdventure();
            Assert.IsNotNull(GameObject.Find("Spawn_Clearing"), "The glade around the spawn is in the scene.");
            Assert.IsNotNull(GameObject.Find("Village_Greenery"), "The village greenery is in the scene.");

            // Along the arrival road: past the village notice that stands on it (RuntimeLoreSpawner), the bend,
            // then onto the ring road
            Vector3[] waypoints = { new Vector3(-27.3f, 0f, -26.6f), new Vector3(-25.5f, 0f, -23.4f), new Vector3(-20f, 0f, -17f), new Vector3(-14.5f, 0f, -11.5f) };
            foreach (Vector3 wp in waypoints)
            {
                yield return WalkTo(wp, 12f, false);
                Vector3 left = wp - hero.transform.position;
                left.y = 0f;
                Assert.Less(left.magnitude, 1.2f, $"The hero reached {wp} on the road (stuck at {hero.transform.position} next to {Nearby()}).");
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Hero_WalksUnderTheForestArch_IntoTheForestDoor()
        {
            yield return StartAdventure();

            Transform gate = GameObject.Find("Village_Reference").transform.Find("Area_ForestGate");
            Transform arch = gate.Find("Arch");
            Assert.IsNotNull(gate.Find("Gate_Pillar_W"), "The arch stands on a west pillar.");
            Assert.IsNotNull(gate.Find("Gate_Pillar_E"), "The arch stands on an east pillar.");
            float archUnderside = Bounds(arch.gameObject).min.y;
            Assert.Greater(archUnderside, 4.5f, "The arch is high enough to walk under.");

            // From the north path just inside the village, straight north through the arch
            cc.enabled = false;
            hero.transform.position = new Vector3(5f, 0.1f, 38f);
            cc.enabled = true;
            yield return null;

            bool loading = false;
            Vector3 at = hero.transform.position;
            float stop = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < stop)
            {
                if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
                {
                    loading = true;
                    break;
                }
                at = hero.transform.position;
                StoryPanelUI.CloseIfOpen();
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                cc.Move(Vector3.forward * 5f * dt + Vector3.down * 0.5f * dt);
                yield return null;
            }

            Assert.IsTrue(loading, $"Walking north under the arch enters the forest door (hero stopped at {at} next to {Nearby()}).");
            Bounds archBounds = Bounds(arch.gameObject);
            Assert.That(at.z, Is.InRange(archBounds.min.z - 1.2f, archBounds.max.z + 0.6f), $"The door opens under the arch (hero at {at}).");
            Assert.Greater(archUnderside, at.y + 2.5f, "The hero passes under the arch with room to spare.");
        }

        private IEnumerator StartAdventure()
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

            hero = Object.FindAnyObjectByType<PlayerUnit>();
            cc = hero != null ? hero.GetComponent<CharacterController>() : null;
            if (cc == null) Assert.Inconclusive("The hero has no CharacterController to walk with.");

            // The door ignores the first moments after a scene load
            float settle = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < settle) yield return null;
        }

        private IEnumerator WalkTo(Vector3 wp, float seconds, bool stopWhenLoading)
        {
            float stop = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < stop)
            {
                Vector3 to = wp - hero.transform.position;
                to.y = 0f;
                if (to.magnitude < 0.6f) break;
                if (stopWhenLoading && SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) break;
                StoryPanelUI.CloseIfOpen(); // a story page pauses the game
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                cc.Move(to.normalized * 5f * dt + Vector3.down * 0.5f * dt);
                yield return null;
            }
        }

        private string Nearby()
        {
            string near = "";
            foreach (Collider col in Physics.OverlapSphere(hero.transform.position + Vector3.up, 1.6f))
            {
                if (col.transform.IsChildOf(hero.transform)) continue;
                Transform t = col.transform;
                string path = t.name;
                while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
                near += path + (col.isTrigger ? " (trigger)" : "") + "; ";
            }
            return near;
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
