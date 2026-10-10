using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// The tavern touches of 2026-10-10 (Vili): the door's arch is timber, a soft fire burns in the stone hearth, and every
    /// hero has a find of their own: the rogue picks a 30-gold chest, the mage takes a Scroll of Embers by the bookshelf,
    /// the knight a Greater Health Potion from the bar. Finds are taken once. Set TAVERN_FINDS_SHOTS to save screenshots.
    /// </summary>
    public class TavernHeroFindsPlayTests
    {
        private static readonly string[] SavedKeys = { "CastleOfDice_SaveData", PauseMenuUI.MusicVolumeKey, PauseMenuUI.SfxVolumeKey };
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
        public IEnumerator Warrior_FindsThePotion_OnTheBar() => Run("Assets/Data/Character_Warrior_SirRoland.asset", CharacterClassType.Warrior, "Warrior");

        [UnityTest, Timeout(300000)]
        public IEnumerator Mage_FindsTheScroll_ByTheBookshelf() => Run("Assets/Data/Character_Mage_Elira.asset", CharacterClassType.Mage, "Mage");

        [UnityTest, Timeout(300000)]
        public IEnumerator Rogue_PicksTheChest_For30Gold() => Run("Assets/Data/Character_Rogue_Corvo.asset", CharacterClassType.Rogue, "Rogue");

        private IEnumerator Run(string classAsset, CharacterClassType hero, string tag)
        {
            LogAssert.ignoreFailingMessages = true;
            string shots = Environment.GetEnvironmentVariable("TAVERN_FINDS_SHOTS");
            bool takeShots = !string.IsNullOrEmpty(shots);
            if (takeShots) Directory.CreateDirectory(shots);

            yield return StartAdventure(classAsset);
            yield return EnterTavern();
            Assert.AreEqual(hero, Hero().CharacterClass.ClassType);

            // --- the arch around the door is wooden now, and the hearth burns
            GameObject door = GameObject.Find(TavernDressing.DoorGroupName);
            MeshFilter frame = null;
            foreach (MeshFilter mf in door.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name == TavernDressing.DoorFrameName) frame = mf;
            Assert.IsNotNull(frame, "the door has its frame");
            Assert.AreEqual("TavernDoorFrame_Wood", frame.sharedMesh.name, "the frame is the timber arch");
            Assert.That(frame.GetComponent<MeshRenderer>().sharedMaterials[0].name, Does.StartWith("M_TavernDoor_Frame"));
            Assert.AreEqual(frame.sharedMesh.vertexCount, frame.sharedMesh.uv.Length, "the timber arch has UVs, so the planks tile");

            GameObject fire = GameObject.Find(TavernDressing.HearthFireName);
            Assert.IsNotNull(fire, "a fire burns in the hearth");
            ParticleSystem[] systems = fire.GetComponentsInChildren<ParticleSystem>();
            Assert.AreEqual(3, systems.Length, "flames, a bright core and sparks");
            yield return Frames(20);
            Assert.Greater(systems[0].particleCount, 5, "the flames are alive");
            Bounds firebox = GameObject.Find(TavernDressing.FireboxName).GetComponent<Renderer>().bounds;
            Vector3 fp = fire.transform.position;
            Assert.IsTrue(fp.x > firebox.min.x && fp.x < firebox.max.x && fp.z > firebox.min.z && fp.z < firebox.max.z, "the fire sits inside the firebox");

            // --- the finds: the chest for everyone, the scroll only for the mage, the potion only for the knight
            LockpickInteraction chest = GameObject.Find(TavernHeroFinds.RogueChestName)?.GetComponent<LockpickInteraction>();
            Assert.IsNotNull(chest, "the rogue's chest stands in the tavern for every hero");
            Assert.IsTrue(chest.IsLocked);
            HeroFindPickup scroll = Find(TavernHeroFinds.MageScrollId);
            HeroFindPickup potion = Find(TavernHeroFinds.WarriorPotionId);
            Assert.AreEqual(hero == CharacterClassType.Mage, scroll != null, "only the mage notices the scroll");
            Assert.AreEqual(hero == CharacterClassType.Warrior, potion != null, "only the knight notices the potion");

            Interactable mine = hero == CharacterClassType.Mage ? scroll : hero == CharacterClassType.Warrior ? (Interactable)potion : chest;
            AssertReachable(mine, tag);

            if (takeShots)
            {
                // The review cameras see the whole room: switching the game camera off puts back the walls it cut away
                CameraFollow follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
                if (follow != null) follow.enabled = false;
                yield return Overview(Path.Combine(shots, tag + "_01_Overview.png"), new Vector3(9f, 17f, -2f), new Vector3(-7f, 0f, -2f));
                yield return Overview(Path.Combine(shots, tag + "_02_Door.png"), new Vector3(-3.5f, 4.5f, -1.5f), new Vector3(-6f, 2.6f, -9f));
                yield return Overview(Path.Combine(shots, tag + "_03_Fireplace.png"), new Vector3(-6.2f, 3.2f, -1.0f), new Vector3(-11.5f, 1.6f, -1f));
                Vector3 at = mine.transform.position;
                Vector3 eye = hero == CharacterClassType.Warrior ? at + new Vector3(1.5f, 2.6f, -4f)
                    : hero == CharacterClassType.Mage ? at + new Vector3(-4.5f, 3f, -1.5f) : at + new Vector3(-1.5f, 3.5f, -5f);
                yield return Overview(Path.Combine(shots, tag + "_04_Find.png"), eye, at + Vector3.up * 0.4f);
                if (follow != null) follow.enabled = true;
            }

            // --- the other heroes cannot pick the lock
            InventoryManager inv = InventoryManager.Instance;
            int gold = inv.CurrentGold;
            if (hero != CharacterClassType.Rogue)
            {
                chest.Interact(Hero());
                yield return null;
                Assert.IsTrue(chest.IsLocked, "only the rogue picks the lock");
            }

            // --- taking the find
            if (hero == CharacterClassType.Rogue)
            {
                Assert.AreEqual(30, (int)typeof(LockpickInteraction).GetField("rewardGold", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chest));
                // The minigame needs a player's timing: count it as won
                typeof(LockpickInteraction).GetMethod("HandleLockpickSuccess", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(chest, new object[] { Hero() });
                Assert.IsFalse(chest.IsLocked);
                Assert.AreEqual(gold + 30, inv.CurrentGold, "30 gold in the chest");
            }
            else
            {
                HeroFindPickup find = hero == CharacterClassType.Mage ? scroll : potion;
                ItemSO item = inv.FindItemByID(find.ItemId);
                int before = inv.GetItemCount(item);
                Teleport(NavPointNear(find), Quaternion.identity);
                yield return Frames(5);
                Assert.IsTrue(find.CanInteract(Hero()), "the hero can reach it");
                find.TriggerInteraction(Hero());
                yield return null;
                Assert.AreEqual(before + 1, inv.GetItemCount(item), item.ItemName + " goes in the bag");
                Assert.IsTrue(StoryFlags.Has(HeroFindPickup.FoundFlag(find.FindId)));
                if (takeShots) yield return Shot(Path.Combine(shots, tag + "_05_Taken.png"));
                StoryPanelUI.CloseIfOpen();
            }

            // --- out and back in: the find stays taken (the chest stays open and empty)
            yield return new WaitForSeconds(2.5f); // doors ignore the hero for a moment after arriving
            DoorTeleporter exit = FindDoor(TavernEntrance.VillageSceneName);
            exit.PerformTeleport(Hero().gameObject);
            yield return Frames(2);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            yield return EnterTavern();
            chest = GameObject.Find(TavernHeroFinds.RogueChestName).GetComponent<LockpickInteraction>();
            if (hero == CharacterClassType.Rogue)
            {
                Assert.IsFalse(chest.IsLocked, "the picked chest stays open");
                if (takeShots)
                {
                    UnityEngine.Object.FindAnyObjectByType<CameraFollow>().enabled = false;
                    yield return Overview(Path.Combine(shots, tag + "_05_Opened.png"), chest.transform.position + new Vector3(-1.5f, 3.5f, -5f), chest.transform.position + Vector3.up * 0.4f);
                }
            }
            else
            {
                Assert.IsNull(Find(hero == CharacterClassType.Mage ? TavernHeroFinds.MageScrollId : TavernHeroFinds.WarriorPotionId), "taken once");
            }
        }

        private static HeroFindPickup Find(string id)
        {
            GameObject go = GameObject.Find("HeroFind_" + id);
            return go != null ? go.GetComponent<HeroFindPickup>() : null;
        }

        /// <summary>The hero can walk from the door to a spot within reach of <paramref name="target"/>.</summary>
        private static void AssertReachable(Interactable target, string what)
        {
            Vector3 start = GameObject.Find(TavernEntrance.SpawnInTavern).transform.position;
            Assert.IsTrue(RuntimeNavMesh.EnsureCovers(start, target.transform.position, Hero().transform), "the NavMesh builds");
            Assert.IsTrue(NavMesh.SamplePosition(start, out NavMeshHit a, 2f, NavMesh.AllAreas), "door on the NavMesh");
            Vector3 near = NavPointNear(target);
            Assert.LessOrEqual(target.DistanceTo(near), target.InteractionRadius, what + ": a walkable spot within reach");
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(a.position, near, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, what + ": a full path from the door");
        }

        private static Vector3 NavPointNear(Interactable target)
        {
            Vector3 p = target.transform.position;
            float best = float.MaxValue;
            Vector3 bestPoint = p;
            for (float r = 0.5f; r <= 3f; r += 0.25f)
            {
                for (int i = 0; i < 24; i++)
                {
                    float ang = i * Mathf.PI * 2f / 24f;
                    Vector3 probe = new Vector3(p.x + Mathf.Cos(ang) * r, 0f, p.z + Mathf.Sin(ang) * r);
                    if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                    if (hit.position.y > 1f) continue; // the ground floor, not the gallery
                    float d = target.DistanceTo(hit.position);
                    if (d < best) { best = d; bestPoint = hit.position; }
                }
                if (best <= target.InteractionRadius - 0.3f) break;
            }
            return bestPoint;
        }

        private static IEnumerator StartAdventure(string classAsset)
        {
            SceneManager.LoadScene(TavernEntrance.VillageSceneName);
            yield return Frames(10);
            MainMenuController menu = MainMenuController.Instance ?? UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "main menu");
#if UNITY_EDITOR
            CharacterClassSO cls = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterClassSO>(classAsset);
#else
            CharacterClassSO cls = null;
#endif
            menu.SelectCharacterClass(cls);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            yield return null;
        }

        private static IEnumerator EnterTavern()
        {
            // The new adventure may still be loading the village: wait for its tavern door
            DoorTeleporter villageDoor = null;
            float until = Time.realtimeSinceStartup + 30f;
            while ((villageDoor = FindDoor(TavernEntrance.SceneName)) == null && Time.realtimeSinceStartup < until) yield return null;
            yield return WaitForLoad();
            Assert.IsNotNull(villageDoor, "the village door into the tavern");
            yield return new WaitForSeconds(1.2f);
            villageDoor.PerformTeleport(Hero().gameObject);
            yield return Frames(2);
            yield return WaitForLoad();
            Assert.AreEqual(TavernEntrance.SceneName, SceneManager.GetActiveScene().name, "inside the tavern");
            yield return Frames(20);
        }

        private static PlayerUnit Hero() => UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

        private static DoorTeleporter FindDoor(string targetScene)
        {
            foreach (DoorTeleporter d in UnityEngine.Object.FindObjectsByType<DoorTeleporter>(FindObjectsSortMode.None))
                if (d.TargetSceneName == targetScene) return d;
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
            CameraFollow follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
            Camera cam = follow != null ? follow.GetComponent<Camera>() : Camera.main;
            if (cam != null) Render(cam, file);
        }

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
            var rt = new RenderTexture(w, h, 24);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
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
