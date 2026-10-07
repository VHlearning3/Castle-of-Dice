using System;
using System.Collections;
using System.IO;
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
    /// Castle Hall: the hero walks from Spawn_From_Tower into the spiral stairs and arrives in the Tower; coming
    /// back down lands them in front of the stairs again. Also checks the stairs' sign and braziers and that the
    /// gloomy Throne Room door still swings open. With HALL_SHOTS_DIR set, saves review screenshots.
    /// </summary>
    public class HallStairsPlayTests
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
        public IEnumerator Hero_WalksUpTheSpiralStairsIntoTheTower()
        {
            LogAssert.ignoreFailingMessages = true;
            string shots = Environment.GetEnvironmentVariable("HALL_SHOTS_DIR");
            bool takeShots = !string.IsNullOrEmpty(shots);
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

            SceneLoader.Instance.LoadScene("Zone_5_CastleHall", "Spawn_From_Tower");
            yield return Frames(2);
            yield return WaitForLoad();
            Assert.AreEqual("Zone_5_CastleHall", SceneManager.GetActiveScene().name);
            StoryPanelUI.CloseIfOpen();
            GameObject spawn = GameObject.Find("Spawn_From_Tower");
            Assert.Less(Flat(Hero().transform.position - spawn.transform.position), 2.5f, "the hero starts in front of the stairs");

            DoorTeleporter stairs = FindDoor("Zone_6_Tower");
            Transform gate = stairs.transform.parent;
            Assert.IsNotNull(gate.Find("Stairs_Model"), "the Tower exit is the spiral staircase");
            ZoneExitSign sign = gate.GetComponentInChildren<ZoneExitSign>();
            Assert.IsNotNull(sign, "the stairs carry the zone sign");
            Assert.AreEqual(2, sign.BrazierCount, "a brazier on each buttress");
            Assert.Greater(sign.transform.position.y, 6f, "the sign hangs above the arch");
            Assert.Less(sign.transform.position.y, 9.5f, "and not up by the turret roof");

            SwingDoor throneDoor = FindDoor("Zone_7_ThroneRoom").transform.parent.GetComponentInChildren<SwingDoor>();
            Assert.IsNotNull(throneDoor, "the Throne Room door still swings");

            if (takeShots)
            {
                yield return Frames(20);
                yield return Shot(Path.Combine(shots, "hall_stairs_from_spawn.png"));
                Teleport(new Vector3(0f, Hero().transform.position.y, 26f), Quaternion.Euler(0f, 0f, 0f));
                yield return Frames(60);
                yield return Shot(Path.Combine(shots, "hall_throne_door.png"));
                Teleport(spawn.transform.position, spawn.transform.rotation);
                yield return Frames(10);
            }

            // Walk straight at the stairs; the first steps take the hero up to the Tower
            Vector3 dir = gate.forward;
            float walkUntil = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name == "Zone_5_CastleHall" && Time.realtimeSinceStartup < walkUntil)
            {
                CharacterController cc = Hero() != null ? Hero().GetComponent<CharacterController>() : null;
                if (cc != null && cc.enabled) cc.Move(dir * (4f * Time.deltaTime) + Vector3.down * 0.05f);
                yield return null;
            }
            yield return WaitForLoad();
            Assert.AreEqual("Zone_6_Tower", SceneManager.GetActiveScene().name, "walking onto the stairs leads to the Tower");
            Assert.AreEqual(GameLocation.Tower, GameManager.Instance.CurrentLocation);

            // The Tower's way back down lands the hero in front of the stairs
            DoorTeleporter down = FindDoor("Zone_5_CastleHall");
            Assert.IsNotNull(down, "the Tower leads back to the hall");
            Assert.AreEqual("Spawn_From_Tower", down.TargetSpawnPointName);
            yield return new WaitForSeconds(1.2f);
            down.PerformTeleport(Hero().gameObject);
            yield return Frames(2);
            yield return WaitForLoad();
            Assert.AreEqual("Zone_5_CastleHall", SceneManager.GetActiveScene().name);
            spawn = GameObject.Find("Spawn_From_Tower");
            Assert.Less(Flat(Hero().transform.position - spawn.transform.position), 2.5f, "back at the foot of the stairs");
            yield return Frames(30);
            Assert.AreEqual("Zone_5_CastleHall", SceneManager.GetActiveScene().name, "arriving does not bounce the hero straight back up");
        }

        private static PlayerUnit Hero() => UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        private static DoorTeleporter FindDoor(string targetScene)
        {
            foreach (DoorTeleporter door in UnityEngine.Object.FindObjectsByType<DoorTeleporter>(FindObjectsSortMode.None))
            {
                if (door.TargetSceneName == targetScene) return door;
            }
            return null;
        }

        private static void Teleport(Vector3 position, Quaternion rotation)
        {
            PlayerExplorationMovement move = Hero().GetComponent<PlayerExplorationMovement>();
            if (move != null) move.TeleportTo(position, rotation);
            else Hero().transform.position = position;
        }

        private static IEnumerator Shot(string file)
        {
            yield return null;
            Camera cam = Camera.main;
            if (cam == null)
            {
                CameraFollow follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
                cam = follow != null ? follow.GetComponent<Camera>() : UnityEngine.Object.FindAnyObjectByType<Camera>();
            }
            if (cam == null) yield break;
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
