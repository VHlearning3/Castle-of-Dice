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
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.UI;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests.PlayMode
{
    /// <summary>
    /// Vili's layout changes of 2026-10-06: the Shadow Witch's fight starts only inside the small square in the
    /// middle of the Grand Archives and its grid stays there; the enlarged Forest Path has a branch path east to a
    /// cottage with a pond, the pond can't be walked into and nobody gets behind the lockpick gate at the west wall.
    /// With LAYOUT_SHOTS_DIR set, saves review screenshots.
    /// </summary>
    public class ForestWitchLayoutPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;
        private string shots;

        [SetUp]
        public void BackupSave()
        {
            saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
            shots = Environment.GetEnvironmentVariable("LAYOUT_SHOTS_DIR");
            if (!string.IsNullOrEmpty(shots)) Directory.CreateDirectory(shots);
        }

        [TearDown]
        public void RestoreSave()
        {
            Time.timeScale = 1f;
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator WitchFight_StartsOnlyInTheSquare()
        {
            yield return NewAdventureIn("Zone_4_Library");
            DungeonRoomController room = UnityEngine.Object.FindAnyObjectByType<DungeonRoomController>();
            Assert.IsNotNull(room);
            Rect area = room.GetGridArea();
            Assert.Less(area.width, 34f, "the fight area is the small square, not the whole hall");

            // Walk in from the hall door along the middle of the room
            Teleport(new Vector3(30f, Hero().transform.position.y, area.center.y));
            yield return Frames(5);
            bool shotOutside = false;
            float startedAtX = float.NaN;
            float until = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < until)
            {
                Vector3 hero = Hero().transform.position;
                if (room.IsAwaitingBossDialogue || room.CurrentState != RoomState.Unexplored) { startedAtX = hero.x; break; }
                if (!shotOutside && hero.x < area.xMax + 3f)
                {
                    shotOutside = true;
                    yield return Shot("witch_approach.png");
                }
                CharacterController cc = Hero().GetComponent<CharacterController>();
                if (cc != null && cc.enabled) cc.Move(new Vector3(-4f * Time.deltaTime, -0.05f, 0f));
                yield return null;
            }
            Assert.IsFalse(float.IsNaN(startedAtX), "the Witch's encounter starts when the hero walks into the square");
            Assert.LessOrEqual(startedAtX, area.xMax + 1f, $"not before the square (started at x {startedAtX:F1}, square edge {area.xMax:F1})");

            // Her intro ends, the fight begins on a grid inside the square
            if (room.IsAwaitingBossDialogue && DialogueController.Instance != null) DialogueController.Instance.EndDialogue();
            yield return Frames(10);
            Assert.AreEqual(RoomState.CombatActive, room.CurrentState);
            GridManager grid = GridManager.Instance;
            Assert.IsNotNull(grid);
            Assert.Greater(grid.Tiles.Count, 0);
            float half = grid.EffectiveTileSize * 0.5f - 0.01f;
            foreach (GridTile tile in grid.Tiles.Values)
            {
                Vector3 p = tile.transform.position;
                Assert.IsTrue(p.x - half >= area.xMin && p.x + half <= area.xMax && p.z - half >= area.yMin && p.z + half <= area.yMax,
                    $"tile at {p} lies outside the square {area}");
            }
            yield return Frames(20);
            yield return Shot("witch_fight.png");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Forest_CottagePathPondAndGate()
        {
            yield return NewAdventureIn("Zone_2_ForestPath");
            foreach (DungeonRoomController room in UnityEngine.Object.FindObjectsByType<DungeonRoomController>(FindObjectsSortMode.None))
            {
                room.gameObject.SetActive(false);
            }

            // Three fights, spread along the longer path
            Assert.AreEqual(3, UnityEngine.Object.FindObjectsByType<DungeonRoomController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            GameObject arrival = GameObject.Find("Spawn_From_Village");
            Assert.Less(arrival.transform.position.z, -80f, "the village arch moved south with the forest's edge");

            // The branch path east to the cottage is walkable all the way to its door
            Vector3[] branch =
            {
                new Vector3(1.5f, 0f, -7f), new Vector3(12f, 0f, -4f), new Vector3(24f, 0f, -8f), new Vector3(36f, 0f, -10f),
                new Vector3(48f, 0f, -6f), new Vector3(58f, 0f, -8f), new Vector3(61f, 0f, -8f)
            };
            Teleport(new Vector3(0f, Hero().transform.position.y, -12f));
            yield return Frames(5);
            yield return WalkTo(branch, 40f, 2f, "cottage_path.png");
            Assert.Less(Flat(Hero().transform.position - new Vector3(61f, 0f, -8f)), 2f, "the hero walked the branch path to the cottage");
            yield return Frames(20);
            yield return Shot("cottage_door.png");

            // The pond north of the house is water, not floor
            GameObject pond = GameObject.Find("Cottage_Pond");
            Assert.IsNotNull(pond);
            Vector3 pondCenter = pond.transform.position;
            Teleport(new Vector3(pondCenter.x - 9f, Hero().transform.position.y, pondCenter.z));
            yield return Frames(5);
            yield return WalkTo(new[] { new Vector3(pondCenter.x, 0f, pondCenter.z) }, 6f, 0.5f, null);
            Assert.Greater(Flat(Hero().transform.position - pondCenter), 5.4f, "the hero stops on the muddy rim, out of the water");
            yield return Frames(10);
            yield return Shot("cottage_pond.png");

            // The lockpick gate stands against the west wall: nobody slips around behind it
            Transform gate = GameObject.Find("Locked_Secret_Gate").transform;
            Teleport(new Vector3(gate.position.x + 1.5f, Hero().transform.position.y, gate.position.z - 10f));
            yield return Frames(5);
            yield return WalkTo(new[] { new Vector3(gate.position.x - 1f, 0f, gate.position.z - 9f), new Vector3(gate.position.x - 1f, 0f, gate.position.z) }, 10f, 0.5f, null);
            Vector3 h = Hero().transform.position;
            Assert.IsFalse(h.x < gate.position.x && Mathf.Abs(h.z - gate.position.z) < 6f, $"the hero got behind the gate ({h})");
            Teleport(new Vector3(gate.position.x + 5f, Hero().transform.position.y, gate.position.z - 3f));
            yield return Frames(20);
            yield return Shot("forest_gate.png");
        }

        #region Helpers

        private IEnumerator NewAdventureIn(string sceneName)
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Zone_1_VillageAndCellar");
            yield return Frames(10);
            CharacterClassSO cls = LoadClass("Character_Warrior_SirRoland");
            MainMenuController menu = MainMenuController.Instance ?? UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "main menu");
            menu.SelectCharacterClass(cls);
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();
            SceneLoader.Instance.LoadScene(sceneName);
            yield return Frames(2);
            yield return WaitForLoad();
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
            StoryPanelUI.CloseIfOpen();
        }

        private IEnumerator WalkTo(Vector3[] points, float seconds, float reach, string midShot)
        {
            int next = 0;
            bool shot = midShot == null;
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                CharacterController cc = Hero().GetComponent<CharacterController>();
                Vector3 to = points[next] - Hero().transform.position;
                to.y = 0f;
                if (to.magnitude < reach)
                {
                    if (next == points.Length - 1) yield break;
                    next++;
                    if (!shot && next >= points.Length / 2)
                    {
                        shot = true;
                        yield return Shot(midShot);
                    }
                    continue;
                }
                if (cc != null && cc.enabled) cc.Move(to.normalized * (6f * Time.deltaTime) + Vector3.down * 0.05f);
                yield return null;
            }
        }

        private static PlayerUnit Hero() => UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        private static void Teleport(Vector3 position)
        {
            PlayerExplorationMovement move = Hero().GetComponent<PlayerExplorationMovement>();
            if (move != null) move.TeleportTo(position, Quaternion.Euler(0f, -90f, 0f));
            else Hero().transform.position = position;
        }

        private IEnumerator Shot(string file)
        {
            if (string.IsNullOrEmpty(shots)) yield break;
            yield return null;
            Camera cam = Camera.main;
            if (cam == null)
            {
                CameraFollow follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
                cam = follow != null ? follow.GetComponent<Camera>() : UnityEngine.Object.FindAnyObjectByType<Camera>();
            }
            if (cam == null) yield break;
            const int w = 1600, h = 900;
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(shots, file), tex.EncodeToPNG());
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
