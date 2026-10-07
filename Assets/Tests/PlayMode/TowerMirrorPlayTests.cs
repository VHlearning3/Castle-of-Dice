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
    /// Treasure Tower: the mirror room stays sealed until the Tower challenge is done, then the gold fades, the
    /// north wall sinks and the hero can walk up to the mirror, which reflects the room. Coming back to the
    /// Tower later finds it still open. The way out is the top of the spiral stairs down to the Castle Hall.
    /// With TOWER_SHOTS_DIR set, saves review screenshots.
    /// </summary>
    public class TowerMirrorPlayTests
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
        public IEnumerator ChallengeOpensTheMirrorRoom_AndTheStairsLeadDown()
        {
            LogAssert.ignoreFailingMessages = true;
            string shots = Environment.GetEnvironmentVariable("TOWER_SHOTS_DIR");
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

            SceneLoader.Instance.LoadScene("Zone_6_Tower", "Spawn_From_Hall");
            yield return Frames(2);
            yield return WaitForLoad();
            Assert.AreEqual("Zone_6_Tower", SceneManager.GetActiveScene().name);
            StoryPanelUI.CloseIfOpen();

            TowerMirrorRoom opener = UnityEngine.Object.FindAnyObjectByType<TowerMirrorRoom>();
            Assert.IsNotNull(opener);
            Assert.IsFalse(StoryFlags.Has(TowerChallenge.CompletedFlag), "a new adventure starts with the challenge unsolved");
            Assert.IsFalse(opener.Room.activeSelf, "sealed before the challenge");
            Assert.IsTrue(opener.BlockingWall.gameObject.activeInHierarchy, "the north wall stands");
            Assert.IsTrue(opener.GoldHoard[0].activeInHierarchy, "the gold lies against it");

            DoorTeleporter down = FindDoor("Zone_5_CastleHall");
            Transform gate = down.transform.parent;
            Assert.IsNotNull(gate.Find("Stairs_Model"), "the way down is the spiral staircase");
            ZoneExitSign sign = gate.GetComponentInChildren<ZoneExitSign>();
            Assert.IsNotNull(sign, "the stairs carry the zone sign");
            Assert.AreEqual(2, sign.BrazierCount, "a brazier on each buttress");

            if (takeShots)
            {
                yield return Frames(30);
                yield return Shot(Path.Combine(shots, "tower_arrival.png"));
                Teleport(new Vector3(0f, Hero().transform.position.y, -4f), Quaternion.Euler(0f, 180f, 0f));
                yield return Frames(60);
                yield return Shot(Path.Combine(shots, "tower_stairs_top.png"));
                Teleport(new Vector3(0f, Hero().transform.position.y, 2f), Quaternion.Euler(0f, 0f, 0f));
                yield return Frames(60);
                yield return Shot(Path.Combine(shots, "tower_sealed_north.png"));
            }

            // Finish the challenge: the runes and the guardian
            TowerChallenge challenge = UnityEngine.Object.FindAnyObjectByType<TowerChallenge>();
            StoryFlags.Set(TowerChallenge.RunesSolvedFlag);
            StoryFlags.Set(TowerChallenge.GuardianDefeatedFlag);
            challenge.TryComplete();
            yield return Frames(2);
            Assert.IsTrue(opener.Room.activeSelf, "the room appears as the wall goes");
            if (takeShots)
            {
                yield return new WaitForSeconds(1.4f);
                yield return Shot(Path.Combine(shots, "tower_wall_sinking.png"));
            }
            yield return new WaitForSeconds(4f);
            Assert.IsFalse(opener.BlockingWall.gameObject.activeInHierarchy, "the wall has sunk away");
            Assert.IsFalse(opener.GoldHoard[0].activeInHierarchy, "the gold is gone");

            // Click-to-move finds a way through the opening to the mirror
            Vector3 from = new Vector3(0f, 0f, -4f), to = new Vector3(0f, 0f, 30f);
            Assert.IsTrue(RuntimeNavMesh.EnsureCovers(from, to, Hero().transform));
            var corners = new Vector3[32];
            int n = RuntimeNavMesh.FindPath(from, to, corners);
            Assert.Greater(n, 0, "a path into the mirror room");
            Assert.Less(Vector3.Distance(corners[n - 1], to), 1f, "the path reaches the mirror");

            // Walk in front of the mirror: it reflects
            PlanarMirror mirror = opener.Room.GetComponentInChildren<PlanarMirror>();
            Teleport(new Vector3(0f, Hero().transform.position.y, 27f), Quaternion.Euler(0f, 0f, 0f));
            yield return Frames(60);
            Assert.IsNotNull(mirror.MirrorCamera, "the mirror's camera woke up");
            Assert.IsTrue(mirror.MirrorCamera.enabled, "and draws while the mirror is in view");
            if (takeShots)
            {
                yield return Shot(Path.Combine(shots, "tower_mirror.png"));
                Teleport(new Vector3(-4f, Hero().transform.position.y, 24f), Quaternion.Euler(0f, 20f, 0f));
                yield return Frames(60);
                yield return Shot(Path.Combine(shots, "tower_mirror_side.png"));
                Teleport(new Vector3(0f, Hero().transform.position.y, 6f), Quaternion.Euler(0f, 0f, 0f));
                yield return Frames(60);
                yield return Shot(Path.Combine(shots, "tower_opened_north.png"));
            }

            // Back again later: still open
            SceneLoader.Instance.LoadScene("Zone_6_Tower", "Spawn_From_Hall");
            yield return Frames(2);
            yield return WaitForLoad();
            opener = UnityEngine.Object.FindAnyObjectByType<TowerMirrorRoom>();
            Assert.IsTrue(opener.Room.activeSelf, "the room stays open");
            Assert.IsFalse(opener.BlockingWall.gameObject.activeInHierarchy, "the wall stays down");
            Assert.IsFalse(GameObject.Find("Gold_Piles") != null, "the gold stays gone");

            // The landing at the top of the stairs takes the hero down to the hall
            yield return new WaitForSeconds(1.2f);
            gate = FindDoor("Zone_5_CastleHall").transform.parent;
            Vector3 dir = gate.forward;
            float walkUntil = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name == "Zone_6_Tower" && Time.realtimeSinceStartup < walkUntil)
            {
                CharacterController cc = Hero() != null ? Hero().GetComponent<CharacterController>() : null;
                if (cc != null && cc.enabled) cc.Move(dir * (4f * Time.deltaTime) + Vector3.down * 0.05f);
                yield return null;
            }
            yield return WaitForLoad();
            Assert.AreEqual("Zone_5_CastleHall", SceneManager.GetActiveScene().name, "the stairs lead down to the hall");
            GameObject spawn = GameObject.Find("Spawn_From_Tower");
            Assert.Less(Flat(Hero().transform.position - spawn.transform.position), 2.5f, "at the foot of the stairs");
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
            if (cam == null) yield break;
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
