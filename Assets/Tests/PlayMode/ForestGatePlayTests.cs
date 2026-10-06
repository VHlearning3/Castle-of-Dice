using System;
using System.Collections;
using System.IO;
using System.Reflection;
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
    /// The Forest Path secret gate for every class: the hero walks up to the (big) locked gate, opens it
    /// (Corvo through the lockpick minigame, Roland and Elira with their d20 check), walks through the
    /// opening and reaches the Grand Archives. Also checks the zone exits carry a destination sign and the
    /// swamp blossoms glow until gathered. With GATE_SHOTS_DIR set, the Rogue run saves review screenshots.
    /// </summary>
    public class ForestGatePlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;

        [SetUp]
        public void BackupSave()
        {
            saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
        }

        [TearDown]
        public void RestoreSave()
        {
            Time.timeScale = 1f;
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Rogue_PicksGateAndWalksThrough() => Run("Character_Rogue_Corvo");

        [UnityTest, Timeout(600000)]
        public IEnumerator Warrior_BreaksGateAndWalksThrough() => Run("Character_Warrior_SirRoland");

        [UnityTest, Timeout(600000)]
        public IEnumerator Mage_ReadsRuneAndWalksThrough() => Run("Character_Mage_Elira");

        private IEnumerator Run(string classAsset)
        {
            LogAssert.ignoreFailingMessages = true;
            string shots = Environment.GetEnvironmentVariable("GATE_SHOTS_DIR");
            bool takeShots = !string.IsNullOrEmpty(shots) && classAsset.Contains("Rogue");
            if (takeShots) Directory.CreateDirectory(shots);

            // New Adventure, then straight to the Forest Path
            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            yield return Frames(10);
            CharacterClassSO cls = LoadClass(classAsset);
            MainMenuController menu = MainMenuController.Instance ?? UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "main menu");
            Assert.IsNotNull(cls, classAsset);
            menu.SelectCharacterClass(cls);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();

            if (takeShots)
            {
                DoorTeleporter villageExit = FindDoor("Zone_2_ForestPath");
                if (villageExit != null) yield return ShotFrom(villageExit.transform.position + new Vector3(0f, 0f, -7f), Path.Combine(shots, "village_exit_sign.png"));
            }

            SceneLoader.Instance.LoadScene("Zone_2_ForestPath");
            yield return Frames(2);
            yield return WaitForLoad();
            Assert.AreEqual("Zone_2_ForestPath", SceneManager.GetActiveScene().name);
            StoryPanelUI.CloseIfOpen();

            // Fights would freeze exploration; this test is about the gate
            foreach (DungeonRoomController room in UnityEngine.Object.FindObjectsByType<DungeonRoomController>(FindObjectsSortMode.None))
            {
                room.gameObject.SetActive(false);
            }

            // Exits to other zones carry their destination's name
            DoorTeleporter toCourtyard = FindDoor("Zone_3_CastleCourtyard");
            Assert.IsNotNull(toCourtyard, "forest exit to the courtyard");
            ZoneExitSign sign = toCourtyard.transform.parent.GetComponentInChildren<ZoneExitSign>();
            Assert.IsNotNull(sign, "the courtyard exit has a sign");
            Assert.AreEqual("Castle Courtyard", sign.Label.text);
            Assert.AreEqual(2, sign.BrazierCount, "a brazier on each pillar");

            // The swamp blossoms glow while they can be gathered
            ChestRewardInteraction herb = GameObject.Find("SwampHerb_2").GetComponent<ChestRewardInteraction>();
            PickupBeacon beacon = herb.GetComponentInChildren<PickupBeacon>();
            Assert.IsNotNull(beacon, "the blossom has a glow");
            Assert.IsTrue(beacon.IsShowing);

            if (takeShots)
            {
                yield return ShotFrom(toCourtyard.transform.position + new Vector3(0f, 0f, -8f), Path.Combine(shots, "forest_exit_courtyard.png"));
                yield return ShotFrom(herb.transform.position + new Vector3(-2.5f, 0f, -2f), Path.Combine(shots, "forest_herb_glow.png"));
                WorldPickup scrap = UnityEngine.Object.FindAnyObjectByType<WorldPickup>();
                if (scrap != null) yield return ShotFrom(scrap.transform.position + new Vector3(-3f, 0f, -2f), Path.Combine(shots, "forest_scrap_glow.png"));
            }

            herb.Interact(Hero());
            yield return null;
            yield return null;
            Assert.IsFalse(beacon.IsShowing, "the glow goes out once the blossom is gathered");

            // Walk up to the gate from the path side
            LockpickInteraction gate = GameObject.Find("Locked_Secret_Gate").GetComponent<LockpickInteraction>();
            Collider gateBox = gate.GetComponent<Collider>();
            Assert.IsTrue(gate.IsLocked);
            Assert.GreaterOrEqual(gateBox.bounds.size.y, 6f, "the gate was made much bigger");
            float hx = gateBox.bounds.max.x + 0.9f;
            Teleport(new Vector3(hx, Hero().transform.position.y, gate.transform.position.z));
            yield return Frames(5);
            Assert.IsTrue(gate.CanInteract(Hero()), $"standing at the gate ({gate.DistanceTo(Hero().transform.position):F2} m) is in reach");

            if (takeShots) yield return Shot(Path.Combine(shots, "forest_gate_locked.png"));

            Time.timeScale = 3f;
            if (classAsset.Contains("Rogue"))
            {
                gate.Interact(Hero());
                yield return null;
                Assert.IsTrue(LockpickMinigameUI.IsOpen, "the lockpick minigame opens for Corvo");
                yield return PlayMinigame(gate);
            }
            else
            {
                float until = Time.realtimeSinceStartup + 120f;
                while (gate.IsLocked && Time.realtimeSinceStartup < until)
                {
                    Hero().Heal(999);
                    gate.Interact(Hero());
                    float settle = Time.realtimeSinceStartup + 4f;
                    while (gate.IsLocked && Time.realtimeSinceStartup < settle)
                    {
                        if (RerollableRoll.IsAwaitingDecision) RerollableRoll.Accept();
                        yield return null;
                    }
                }
            }
            Time.timeScale = 1f;
            Assert.IsFalse(gate.IsLocked, "the gate opened");
            yield return Frames(5);
            if (takeShots) yield return Shot(Path.Combine(shots, "forest_gate_open.png"));

            // Walk through the opening to the secret door into the Grand Archives
            CharacterController cc = Hero().GetComponent<CharacterController>();
            float walkUntil = Time.realtimeSinceStartup + 25f;
            while (SceneManager.GetActiveScene().name == "Zone_2_ForestPath" && Time.realtimeSinceStartup < walkUntil)
            {
                if (cc != null && cc.enabled && Hero() != null)
                {
                    Vector3 hero = Hero().transform.position;
                    Vector3 step = new Vector3(-1f, 0f, (gate.transform.position.z - hero.z) * 0.5f) * (4f * Time.deltaTime);
                    cc.Move(step);
                }
                yield return null;
            }
            yield return WaitForLoad();
            Assert.AreEqual("Zone_4_Library", SceneManager.GetActiveScene().name, "walked through the gate into the Grand Archives");
        }

        private static IEnumerator PlayMinigame(LockpickInteraction gate)
        {
            LockpickMinigameUI ui = LockpickMinigameUI.Instance;
            FieldInfo gameField = typeof(LockpickMinigameUI).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo handlePress = typeof(LockpickMinigameUI).GetMethod("HandlePress", BindingFlags.Instance | BindingFlags.NonPublic);
            float until = Time.realtimeSinceStartup + 60f;
            while (gate.IsLocked && Time.realtimeSinceStartup < until)
            {
                if (ui != null && gameField.GetValue(ui) is LockpickMinigame game && !game.IsFinished && game.IsMarkerInSweetZone)
                {
                    handlePress.Invoke(ui, new object[] { game.Press() });
                }
                yield return null;
            }
        }

        #region Helpers

        private static PlayerUnit Hero() => UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

        private static DoorTeleporter FindDoor(string targetScene)
        {
            foreach (DoorTeleporter door in UnityEngine.Object.FindObjectsByType<DoorTeleporter>(FindObjectsSortMode.None))
            {
                if (door.TargetSceneName == targetScene) return door;
            }
            return null;
        }

        private static void Teleport(Vector3 position)
        {
            PlayerExplorationMovement move = Hero().GetComponent<PlayerExplorationMovement>();
            if (move != null) move.TeleportTo(position, Quaternion.Euler(0f, -90f, 0f));
            else Hero().transform.position = position;
        }

        private static IEnumerator ShotFrom(Vector3 heroPosition, string file)
        {
            heroPosition.y = Hero().transform.position.y;
            Teleport(heroPosition);
            yield return Frames(20);
            yield return Shot(file);
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
            if (cam == null)
            {
                Debug.LogWarning($"[ForestGatePlayTests] No camera for {file}.");
                yield break;
            }
            const int w = 1600, h = 900;
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = previous;
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

        private static CharacterClassSO LoadClass(string assetName)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterClassSO>($"Assets/Data/{assetName}.asset");
#else
            return null;
#endif
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        #endregion
    }
}
