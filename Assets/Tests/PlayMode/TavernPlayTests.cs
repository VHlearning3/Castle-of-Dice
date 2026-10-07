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
    /// The tavern (Vili, 2026-10-07): the village's tavern door behind Barnaby leads into Zone_Tavern, the hero
    /// can walk up the stairs to the gallery, the elf waitress makes her rounds, and the door inside leads back
    /// out in front of the tavern. With TAVERN_SHOTS_DIR set, saves review screenshots.
    /// </summary>
    public class TavernPlayTests
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
        public IEnumerator Hero_EntersTheTavern_ClimbsToTheGallery_AndLeaves()
        {
            LogAssert.ignoreFailingMessages = true;
            string shots = Environment.GetEnvironmentVariable("TAVERN_SHOTS_DIR");
            bool takeShots = !string.IsNullOrEmpty(shots);
            if (takeShots) Directory.CreateDirectory(shots);

            SceneManager.LoadScene(TavernEntrance.VillageSceneName);
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

            // --- the village door, made at runtime behind Barnaby
            DoorTeleporter villageDoor = FindDoor(TavernEntrance.SceneName);
            Assert.IsNotNull(villageDoor, "the village has a door into the tavern");
            Assert.AreEqual(TavernEntrance.SpawnInTavern, villageDoor.TargetSpawnPointName);
            GameObject barnaby = GameObject.Find("NPC_Barnaby");
            Assert.IsNotNull(barnaby, "Barnaby");
            Assert.Less(Flat(villageDoor.transform.position - barnaby.transform.position), 14f, "the door is the tavern's, right behind Barnaby");
            GameObject outside = GameObject.Find(TavernEntrance.SpawnInVillage);
            Assert.IsNotNull(outside, "the return spot in front of the tavern");
            Assert.IsNotNull(villageDoor.transform.parent.GetComponentInChildren<ZoneExitSign>(), "the door carries a sign");

            Teleport(outside.transform.position, outside.transform.rotation);
            yield return Frames(30);
            if (takeShots)
            {
                Teleport(outside.transform.position + outside.transform.forward * 3f, Quaternion.LookRotation(-outside.transform.forward));
                yield return Frames(40);
                yield return Shot(Path.Combine(shots, "Tavern_01_Village_Door.png"));
                Teleport(outside.transform.position, outside.transform.rotation);
                yield return Frames(10);
            }

            // Walk at the door; stepping onto the steps takes the hero inside
            yield return new WaitForSeconds(1.2f);
            Vector3 toDoor = villageDoor.GetComponent<Collider>().bounds.center - Hero().transform.position;
            toDoor.y = 0f;
            Vector3 dir = toDoor.normalized;
            float walkUntil = Time.realtimeSinceStartup + 20f;
            while (SceneManager.GetActiveScene().name == TavernEntrance.VillageSceneName && Time.realtimeSinceStartup < walkUntil)
            {
                CharacterController cc = Hero() != null ? Hero().GetComponent<CharacterController>() : null;
                if (cc != null && cc.enabled) cc.Move(dir * (4f * Time.deltaTime) + Vector3.down * 0.05f);
                yield return null;
            }
            yield return WaitForLoad();
            Assert.AreEqual(TavernEntrance.SceneName, SceneManager.GetActiveScene().name, "walking to the tavern door leads inside");
            Assert.AreEqual(GameLocation.Tavern, GameManager.Instance.CurrentLocation);
            GameObject arrival = GameObject.Find(TavernEntrance.SpawnInTavern);
            Assert.Less(Flat(Hero().transform.position - arrival.transform.position), 2.5f, "the hero arrives just inside the door");
            yield return Frames(30);
            Assert.AreEqual(TavernEntrance.SceneName, SceneManager.GetActiveScene().name, "arriving does not bounce the hero straight back out");

            // --- the waitress makes her rounds
            TavernWaitress waitress = UnityEngine.Object.FindAnyObjectByType<TavernWaitress>();
            Assert.IsNotNull(waitress, "the elf waitress works here");
            // batch runs draw no frames on their own: keep her animating for the review shots
            waitress.GetComponentInChildren<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Vector3 elfStart = waitress.transform.position;
            float until = Time.realtimeSinceStartup + 12f;
            while (Flat(waitress.transform.position - elfStart) < 1f && Time.realtimeSinceStartup < until) yield return null;
            Assert.Greater(Flat(waitress.transform.position - elfStart), 1f, "she walks to the next table");

            if (takeShots)
            {
                yield return Shot(Path.Combine(shots, "Tavern_02_Arrival.png"));
                yield return Overview(Path.Combine(shots, "Tavern_03_Overview_SW.png"), new Vector3(-17f, 21f, -19f), new Vector3(0f, 1.5f, 0f));
                yield return Overview(Path.Combine(shots, "Tavern_04_Overview_SE.png"), new Vector3(18f, 21f, -18f), new Vector3(-1f, 1.5f, 1f));
                yield return Overview(Path.Combine(shots, "Tavern_05_Overview_NW.png"), new Vector3(-16f, 20f, 17f), new Vector3(2f, 1.5f, -2f));
                Teleport(new Vector3(-2f, 0.1f, -4.5f), Quaternion.Euler(0f, 20f, 0f));
                yield return Frames(60);
                yield return Shot(Path.Combine(shots, "Tavern_06_Tables.png"));
                Teleport(new Vector3(-7f, 0.1f, -0.2f), Quaternion.Euler(0f, -30f, 0f));
                yield return Frames(60);
                yield return Shot(Path.Combine(shots, "Tavern_07_Bar_And_Fire.png"));
                Teleport(new Vector3(1f, 5.3f, 6.2f), Quaternion.Euler(0f, 180f, 0f));
                yield return Frames(60);
                yield return Shot(Path.Combine(shots, "Tavern_08_Gallery.png"));
                Teleport(new Vector3(9.5f, 5.3f, -6.5f), Quaternion.Euler(0f, 250f, 0f));
                yield return Frames(60);
                yield return Shot(Path.Combine(shots, "Tavern_09_Stairs_Top.png"));
                Vector3 elf = waitress.transform.position;
                Teleport(elf + new Vector3(-2.2f, 0.1f, -2.2f), Quaternion.LookRotation(new Vector3(1f, 0f, 1f)));
                yield return Frames(30);
                yield return Overview(Path.Combine(shots, "Tavern_10_Waitress.png"), waitress.transform.position + new Vector3(-3.2f, 3.6f, -4.2f), waitress.transform.position + Vector3.up * 1.8f);
                Teleport(arrival.transform.position, arrival.transform.rotation);
                yield return Frames(10);
            }

            // --- the gallery is reachable up the stairs
            Vector3 gallery = new Vector3(0f, 5.2f, 6.5f);
            Assert.IsTrue(RuntimeNavMesh.EnsureCovers(Hero().transform.position, gallery, Hero().transform), "NavMesh builds");
            var corners = new Vector3[64];
            int count = RuntimeNavMesh.FindPath(Hero().transform.position, gallery, corners);
            Assert.Greater(count, 1, "there is a path to the gallery");
            Assert.Greater(corners[count - 1].y, 4.6f, "the path ends up on the gallery, not under it");

            // --- and back out to the village, in front of the tavern
            DoorTeleporter inside = FindDoor(TavernEntrance.VillageSceneName);
            Assert.IsNotNull(inside, "the tavern's door leads back to the village");
            Assert.AreEqual(TavernEntrance.SpawnInVillage, inside.TargetSpawnPointName);
            yield return new WaitForSeconds(1.2f);
            inside.PerformTeleport(Hero().gameObject);
            yield return Frames(2);
            yield return WaitForLoad();
            Assert.AreEqual(TavernEntrance.VillageSceneName, SceneManager.GetActiveScene().name);
            Assert.AreEqual(GameLocation.Village, GameManager.Instance.CurrentLocation);
            outside = GameObject.Find(TavernEntrance.SpawnInVillage);
            Assert.Less(Flat(Hero().transform.position - outside.transform.position), 2.5f, "back out in front of the tavern");
            yield return Frames(30);
            Assert.AreEqual(TavernEntrance.VillageSceneName, SceneManager.GetActiveScene().name, "leaving does not bounce the hero back in");
            if (takeShots) yield return Shot(Path.Combine(shots, "Tavern_11_Back_Outside.png"));
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
            Render(cam, file);
        }

        /// <summary>A fixed overview camera (the game camera stays with the hero).</summary>
        private static IEnumerator Overview(string file, Vector3 from, Vector3 lookAt)
        {
            yield return null;
            var go = new GameObject("Shot_Camera");
            Camera cam = go.AddComponent<Camera>();
            Camera main = Camera.main;
            if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
            cam.fieldOfView = 50f;
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(lookAt - from);
            Render(cam, file);
            UnityEngine.Object.Destroy(go);
        }

        private static void Render(Camera cam, string file)
        {
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
