using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
    /// Baldur's Gate 3 camera and click-to-move (Vili, 2026-10-07): a click walks the hero around what is in the
    /// way, the zoom tilts the camera, and nothing visible stays between the camera and the hero in the village,
    /// the cellar fight and the castle's rooms. With CAMERA_SHOTS_DIR set, saves review screenshots.
    /// </summary>
    public class Bg3CameraPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;
        private string shots;

        [SetUp]
        public void BackupSave()
        {
            saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
            shots = Environment.GetEnvironmentVariable("CAMERA_SHOTS_DIR");
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
        public IEnumerator Village_ClickToMoveZoomAndCellar()
        {
            yield return NewAdventure();
            CameraFollow follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
            Assert.IsNotNull(follow, "camera rig");
            yield return Frames(20);
            yield return Shot("village_start.png");

            // Zoom: closer is flatter, farther is more top-down
            follow.SetZoom(0f, true);
            yield return Frames(10);
            float closePitch = follow.transform.eulerAngles.x;
            float closeDistance = Vector3.Distance(follow.transform.position, Hero().transform.position);
            yield return Shot("village_zoom_in.png");
            follow.SetZoom(1f, true);
            yield return Frames(10);
            float farPitch = follow.transform.eulerAngles.x;
            float farDistance = Vector3.Distance(follow.transform.position, Hero().transform.position);
            yield return Shot("village_zoom_out.png");
            Assert.Less(closePitch + 15f, farPitch, "zooming out tilts the camera more top-down");
            Assert.Less(closeDistance + 5f, farDistance, "zooming out moves the camera away");
            follow.SetZoom(0.55f, true);

            // Turning: the camera looks along the new heading
            follow.FaceDirection(Vector3.right);
            yield return Frames(10);
            Vector3 fwd = follow.transform.forward;
            fwd.y = 0f;
            Assert.Greater(Vector3.Dot(fwd.normalized, Vector3.right), 0.95f, "the camera faces the new heading");
            yield return Shot("village_turned.png");

            // Click-to-move: the hero walks a NavMesh path to a spot well away
            PlayerExplorationMovement movement = Hero().GetComponent<PlayerExplorationMovement>();
            Vector3 start = Hero().transform.position;
            Assert.IsTrue(RuntimeNavMesh.EnsureCovers(start, start, Hero().transform), "a NavMesh is built around the hero");
            Assert.IsTrue(NavMesh.SamplePosition(start + new Vector3(9f, 0f, 9f), out NavMeshHit goalHit, 6f, NavMesh.AllAreas), "walkable ground near the hero");
            Vector3 goal = goalHit.position;
            Assert.IsTrue(movement.MoveTo(goal), "the hero accepts the order");
            float until = Time.realtimeSinceStartup + 20f;
            while (movement.HasDestination && Time.realtimeSinceStartup < until) yield return null;
            Assert.Less(Flat(Hero().transform.position - goal), 1.2f, $"the hero walked to the clicked spot {goal} (stood at {Hero().transform.position})");
            yield return Shot("village_walked.png");
            AssertViewClear(follow, "village");

            // The cellar: a tight room under the village; its fight starts on arrival
            DungeonRoomController cellar = null;
            foreach (DungeonRoomController room in UnityEngine.Object.FindObjectsByType<DungeonRoomController>(FindObjectsSortMode.None))
            {
                if (room.roomLocation == "Cellar") cellar = room;
            }
            Assert.IsNotNull(cellar, "Barnaby's cellar");
            Rect area = cellar.GetGridArea();
            Vector3 cellarPoint = new Vector3(area.center.x, cellar.transform.position.y + 0.2f, area.center.y);
            if (Physics.Raycast(cellarPoint + Vector3.up * 2.5f, Vector3.down, out RaycastHit floor, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                cellarPoint = floor.point + Vector3.up * 0.05f;
            }
            movement.TeleportTo(cellarPoint, Quaternion.identity);
            follow.SetZoom(0.45f, true);
            yield return Frames(40);
            yield return Shot("cellar.png");
            AssertViewClear(follow, "cellar");
            for (int i = 0; i < 4; i++)
            {
                follow.FaceDirection(Quaternion.Euler(0f, 90f * i, 0f) * Vector3.forward);
                yield return Frames(15);
                AssertViewClear(follow, $"cellar heading {90 * i}");
                yield return Shot($"cellar_{90 * i}.png");
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator CastleRooms_ViewStaysClear()
        {
            string[] zones = { "Zone_4_Library", "Zone_5_CastleHall", "Zone_6_Tower", "Zone_7_ThroneRoom" };
            yield return NewAdventure();
            foreach (string zone in zones)
            {
                SceneLoader.Instance.LoadScene(zone);
                yield return Frames(2);
                yield return WaitForLoad();
                StoryPanelUI.CloseIfOpen();
                Assert.AreEqual(zone, SceneManager.GetActiveScene().name);

                CameraFollow follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
                Assert.IsNotNull(follow, zone);
                for (int i = 0; i < 4; i++)
                {
                    follow.FaceDirection(Quaternion.Euler(0f, 90f * i, 0f) * Vector3.forward);
                    yield return Frames(15);
                    AssertViewClear(follow, $"{zone} heading {90 * i}");
                    if (i % 2 == 0) yield return Shot($"{zone}_{90 * i}.png");
                }
            }
        }

        /// <summary>
        /// Vili, 2026-10-07: arriving through a castle door left the hero hidden behind the door frame and its
        /// "To ..." sign, which have no colliders, so the check above missed them. Every door arrival, with the
        /// zone's own starting view: no mesh is drawn between the camera and the hero.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator DoorArrivals_HeroStaysInView()
        {
            string[,] arrivals =
            {
                { "Zone_2_ForestPath", "Spawn_From_Village" },
                { "Zone_2_ForestPath", "Spawn_From_Courtyard" },
                { "Zone_2_ForestPath", "Spawn_From_SecretPath" },
                { "Zone_2.5_SecretForestPath", "Spawn_From_ForestPath" },
                { "Zone_2.5_SecretForestPath", "Spawn_From_Library" },
                { "Zone_4_Library", "Spawn_From_Hall" },
                { "Zone_4_Library", "Spawn_From_SecretForestPath" },
                { "Zone_5_CastleHall", "Spawn_From_Courtyard" },
                { "Zone_5_CastleHall", "Spawn_From_Library" },
                { "Zone_5_CastleHall", "Spawn_From_ThroneRoom" },
                { "Zone_5_CastleHall", "Spawn_From_Tower" },
                { "Zone_6_Tower", "Spawn_From_Hall" },
                { "Zone_7_ThroneRoom", "Spawn_From_Hall" },
                { "Zone_3_CastleCourtyard", "Spawn_From_Forest" },
                { "Zone_3_CastleCourtyard", "Spawn_From_Hall" }, // last: the Cursed Commander greets the hero here
            };
            yield return NewAdventure();
            var problems = new System.Collections.Generic.List<string>();
            for (int i = 0; i < arrivals.GetLength(0); i++)
            {
                string zone = arrivals[i, 0];
                string spawn = arrivals[i, 1];
                SceneLoader.Instance.LoadScene(zone, spawn);
                yield return Frames(2);
                yield return WaitForLoad();
                StoryPanelUI.CloseIfOpen();
                Assert.AreEqual(zone, SceneManager.GetActiveScene().name);
                yield return new WaitForSecondsRealtime(0.5f);

                CameraFollow follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
                Assert.IsNotNull(follow, zone);
                string blocker = FirstMeshInView(follow);
                if (blocker != null) problems.Add($"{zone} from {spawn}: {blocker}");
                yield return Shot($"arrival_{zone}_{spawn}.png");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        #region Helpers

        /// <summary>
        /// The first mesh drawn between the camera and the hero (head, waist or feet), found by its own box (doorways,
        /// signs and their text have no colliders), or null. Ground, roads and battlement rings are too big to be in the way.
        /// </summary>
        private static string FirstMeshInView(CameraFollow follow)
        {
            Transform hero = Hero().transform;
            Vector3 cam = follow.transform.position;
            float[] heights = { 1.2f, 0.8f, 0.2f };
            foreach (MeshRenderer rend in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!rend.enabled || !rend.gameObject.activeInHierarchy || rend.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                if (rend.transform.IsChildOf(hero) || rend.GetComponentInParent<CombatUnit>() != null) continue;
                Bounds box = rend.bounds;
                if (Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z)) > 24f) continue;

                Matrix4x4 toLocal = rend.transform.worldToLocalMatrix;
                Bounds local = rend.localBounds;
                Vector3 localCam = toLocal.MultiplyPoint3x4(cam);
                foreach (float height in heights)
                {
                    Vector3 point = hero.position + Vector3.up * height;
                    if (!CameraFollow.SegmentHitsBox(point, cam, box)) continue;
                    Vector3 localPoint = toLocal.MultiplyPoint3x4(point);
                    if (local.Contains(localPoint)) continue; // a room shell around the hero pulls the camera in instead
                    if (!CameraFollow.SegmentHitsBox(localPoint, localCam, local)) continue;
                    return $"'{PathOf(rend.transform)}' between the camera {cam} and the hero {point}";
                }
            }
            return null;
        }

        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        /// <summary>Nothing drawn stands between the camera and the hero's head (cut-away walls are drawn as shadow only).</summary>
        private static void AssertViewClear(CameraFollow follow, string where)
        {
            Vector3 head = Hero().transform.position + Vector3.up * 1.2f;
            Vector3 cam = follow.transform.position;
            RaycastHit[] hits = Physics.RaycastAll(head, (cam - head).normalized, Vector3.Distance(head, cam), ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                Collider col = hit.collider;
                if (col.transform.IsChildOf(Hero().transform) || CameraFollow.IsCutAway(col)) continue;
                if (col.GetComponentInParent<CombatUnit>() != null) continue;
                if (col.gameObject.layer == 8 || col.gameObject.layer == 9 || col.gameObject.layer == 6) continue;
                Renderer rend = col.GetComponent<Renderer>();
                if (rend == null || !rend.enabled || rend.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                Assert.Fail($"{where}: '{col.name}' blocks the view between the camera {cam} and the hero {head}");
            }
        }

        private static GameObject Hero()
        {
            PlayerUnit unit = UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();
            return unit != null ? unit.gameObject : null;
        }

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        private IEnumerator NewAdventure()
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
            yield return Frames(10);
        }

        private IEnumerator Shot(string file)
        {
            if (string.IsNullOrEmpty(shots)) yield break;
            yield return null;
            Camera cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
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
