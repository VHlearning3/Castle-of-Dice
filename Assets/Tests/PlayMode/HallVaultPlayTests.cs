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
    /// Castle Hall vault: the iron door stays shut until Sir Aldric's riddle is answered, then swings open, the
    /// chest appears inside and the hero can walk in to it. The ghost drifts. With HALL_SHOTS_DIR set, saves shots.
    /// </summary>
    public class HallVaultPlayTests
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
        public IEnumerator Riddle_OpensTheIronDoorAndTheHeroWalksIn()
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
            CharacterClassSO cls = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterClassSO>("Assets/Data/Character_Rogue_Corvo.asset");
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

            HallVault vault = UnityEngine.Object.FindAnyObjectByType<HallVault>(FindObjectsInactive.Include);
            Assert.IsFalse(vault.IsOpen, "a new adventure starts with the vault shut");
            Assert.IsTrue(vault.Door.activeSelf, "the doorway is blocked");
            Assert.IsFalse(vault.RewardChest.activeSelf, "no chest before the riddle");
            Quaternion shut = vault.DoorLeaf.localRotation;

            // The ghost drifts
            GameObject ghost = GameObject.Find("Ghost_Of_Sir_Aldric");
            Transform visual = ghost.transform.Find("Visuals");
            float y0 = visual.localPosition.y;
            float spread = 0f;
            float watchUntil = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < watchUntil)
            {
                yield return null;
                spread = Mathf.Max(spread, Mathf.Abs(visual.localPosition.y - y0));
            }
            Assert.Greater(spread, 0.01f, $"Sir Aldric bobs in the air (timeScale {Time.timeScale})");

            Vector3 doorway = vault.Door.transform.position;
            Teleport(new Vector3(doorway.x - 1f, Hero().transform.position.y, doorway.z + 6f), Quaternion.Euler(0f, 180f, 0f));
            yield return Frames(60);
            if (takeShots) yield return Shot(Path.Combine(shots, "vault_shut_ghost.png"));

            // Riddle answered: the door swings open and the chest is there
            HallVault.OpenInScene();
            yield return new WaitForSeconds(2.2f);
            Assert.IsTrue(vault.IsOpen);
            Assert.IsFalse(vault.Door.activeSelf, "the doorway is free");
            Assert.IsTrue(vault.RewardChest.activeSelf, "the chest waits inside");
            Assert.Greater(Quaternion.Angle(shut, vault.DoorLeaf.localRotation), 95f, "the iron door stands open");

            // Walk in through the doorway to the chest
            ChestRewardInteraction chest = vault.RewardChest.GetComponent<ChestRewardInteraction>();
            Vector3[] path = { new Vector3(doorway.x, 0f, doorway.z + 1.5f), new Vector3(doorway.x, 0f, doorway.z - 1.5f), chest.transform.position + new Vector3(0f, 0f, 1.6f) };
            int next = 0;
            float until = Time.realtimeSinceStartup + 25f;
            while (next < path.Length && Time.realtimeSinceStartup < until)
            {
                CharacterController cc = Hero().GetComponent<CharacterController>();
                Vector3 to = path[next] - Hero().transform.position;
                to.y = 0f;
                if (to.magnitude < 0.4f) { next++; continue; }
                if (cc != null && cc.enabled) cc.Move(to.normalized * (4f * Time.deltaTime) + Vector3.down * 0.05f);
                yield return null;
            }
            Assert.AreEqual(path.Length, next, $"walked into the vault (stuck at {Hero().transform.position})");
            Assert.IsTrue(chest.CanInteract(Hero()), "the chest is in reach");
            if (takeShots)
            {
                yield return Frames(30);
                yield return Shot(Path.Combine(shots, "vault_open_inside.png"));
            }
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
