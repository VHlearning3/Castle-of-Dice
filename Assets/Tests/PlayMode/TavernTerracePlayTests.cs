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
    /// The tavern terrace (Vili, 2026-10-09): the deck in front of The Foaming Tankard covers the yard from the steps
    /// to the plaza, four tables sit under two striped pergolas with a wide aisle to the door, and the cellar hatch
    /// moved to the left of the tavern under its own canopy. Canopy roofs let clicks through and are cut away over
    /// the hero. Set TERRACE_SHOTS to a folder to save screenshots.
    /// </summary>
    public class TavernTerracePlayTests
    {
        private static readonly string[] SavedKeys = { "CastleOfDice_SaveData", PauseMenuUI.MusicVolumeKey, PauseMenuUI.SfxVolumeKey };
        private readonly string[] backup = new string[3];
        private readonly bool[] had = new bool[3];

        private Vector3 tavern;
        private Vector3 front;
        private Vector3 right;

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
        public IEnumerator Terrace_IsWalkable_CanopiesClickThrough_CellarMoved()
        {
            LogAssert.ignoreFailingMessages = true;
            string shots = Environment.GetEnvironmentVariable("TERRACE_SHOTS");
            bool takeShots = !string.IsNullOrEmpty(shots);
            if (takeShots) Directory.CreateDirectory(shots);

            yield return StartAdventure();
            Transform building = GameObject.Find("Village_Reference/Area_Tavern/Tavern").transform;
            tavern = building.position;
            Quaternion yaw = Quaternion.Euler(0f, building.eulerAngles.y, 0f);
            front = yaw * Vector3.back;
            right = yaw * Vector3.left;

            // The tavern door is still found from Barnaby, and the hero comes out into the open aisle
            GameObject outside = GameObject.Find(TavernEntrance.SpawnInVillage);
            Assert.IsNotNull(outside, "the tavern door and its return spot are built");
            Assert.Less(Mathf.Abs(Vector3.Dot(outside.transform.position - tavern, right)), 1.0f, "the return spot is in the middle of the aisle");

            // Paths from the tavern door: to the plaza, around every table and to the cellar hatch
            Vector3 start = outside.transform.position;
            Vector3 plaza = At(15f, 0f);
            Assert.IsTrue(RuntimeNavMesh.EnsureCovers(start, plaza, Hero().transform), "the NavMesh builds");
            AssertWalk(start, plaza, "to the plaza");
            foreach (float r in new[] { 4f, -4f })
            {
                AssertWalk(start, At(8.35f, r), $"between the tables at r {r}");
                AssertWalk(start, At(8.35f, r * 1.58f), $"along the outer edge at r {r}");
                AssertWalk(start, At(12.45f, r), $"behind the front table at r {r}");
            }
            GameObject exitPoint = GameObject.Find("Village_PlayerExitPoint");
            AssertWalk(start, exitPoint.transform.position, "to the cellar hatch");

            // The hatch sits left of the tavern under its canopy; the ladder brings the hero back in front of it
            GameObject hatch = GameObject.Find("Village_Cellar_Hatch");
            Vector3 hatchLocal = Local(hatch.transform.position);
            Assert.Greater(hatchLocal.x, 5.5f, "the hatch is beside the tavern's left wall");
            Assert.Less(hatchLocal.z, 4.5f, "the hatch is level with the tavern front");
            GameObject ladder = GameObject.Find("Cellar_Exit_Ladder");
            Assert.AreSame(exitPoint.transform, ladder.GetComponent<DoorTeleporter>().Destination, "the ladder leads to the exit point");
            Assert.Less(Flat(exitPoint.transform.position - hatch.transform.position), 3.5f, "the exit point is in front of the hatch");
            Assert.IsTrue(NavMesh.SamplePosition(exitPoint.transform.position, out NavMeshHit exitHit, 1.6f, NavMesh.AllAreas), "the exit point is on open ground");
            Assert.Less(Flat(exitHit.position - exitPoint.transform.position), 0.6f, "nothing stands on the exit point");

            // Canopy roofs: clicks pass through to the hatch, the camera cuts a roof away over the hero
            Teleport(exitPoint.transform.position + front * 1.5f, Quaternion.LookRotation(-front));
            yield return Frames(40);
            Camera cam = GameCamera();
            Assert.IsTrue(FirstClickable(cam, hatch.GetComponent<Collider>().bounds.center) == hatch.GetComponent<Collider>(), "a click on the hatch reaches it through the canopy");
            if (takeShots) yield return Shot(cam, Path.Combine(shots, "game_cellar.png"));

            Teleport(At(8.35f, 4f), Quaternion.LookRotation(front));
            yield return Frames(40);
            Collider roof = GameObject.Find("Village_Reference/Area_Tavern/Terrace/Canopy_Left/Roof").GetComponent<Collider>();
            Assert.IsTrue(CameraFollow.IsCutAway(roof), "the canopy over the hero is cut away");
            if (takeShots) yield return Shot(cam, Path.Combine(shots, "game_under_canopy.png"));

            Teleport(At(7.7f, 0f), Quaternion.LookRotation(front));
            yield return Frames(40);
            if (takeShots) yield return Shot(cam, Path.Combine(shots, "game_aisle.png"));

            // Down the hatch and back up the ladder
            yield return new WaitForSeconds(2.5f);
            hatch.GetComponent<DoorTeleporter>().Interact(Hero());
            yield return Frames(30);
            Assert.Less(Hero().transform.position.y, -20f, "the hatch takes the hero down into the cellar");
        }

        private void AssertWalk(Vector3 from, Vector3 to, string what)
        {
            Assert.IsTrue(NavMesh.SamplePosition(from, out NavMeshHit a, 2f, NavMesh.AllAreas), what + ": start on the NavMesh");
            Assert.IsTrue(NavMesh.SamplePosition(to, out NavMeshHit b, 2f, NavMesh.AllAreas), what + ": goal on the NavMesh");
            Assert.Less(Flat(b.position - to), 0.7f, what + ": the goal spot is open");
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, what + ": a full path");
        }

        private Vector3 At(float f, float r) => tavern + front * f + right * r;

        private Vector3 Local(Vector3 world)
        {
            Vector3 d = world - tavern;
            return new Vector3(Vector3.Dot(d, right), 0f, Vector3.Dot(d, front));
        }

        private static Collider FirstClickable(Camera cam, Vector3 target)
        {
            Ray ray = new Ray(cam.transform.position, (target - cam.transform.position).normalized);
            RaycastHit[] hits = Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(Hero().transform)) continue;
                if (CameraFollow.IsCutAway(h.collider)) continue;
                if (h.collider.GetComponent<ClickThrough>() != null) continue;
                if (h.collider.GetComponentInParent<Interactable>() != null) return h.collider;
                if (h.collider.isTrigger) continue;
                return h.collider;
            }
            return null;
        }

        private static IEnumerator StartAdventure()
        {
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
            float until = Time.realtimeSinceStartup + 30f;
            while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.realtimeSinceStartup < until) yield return null;
            yield return Frames(10);
            StoryPanelUI.CloseIfOpen();
            yield return null;
        }

        private static PlayerUnit Hero() => UnityEngine.Object.FindAnyObjectByType<PlayerUnit>();

        private static Camera GameCamera()
        {
            CameraFollow follow = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
            return follow != null ? follow.GetComponent<Camera>() : Camera.main;
        }

        private static void Teleport(Vector3 position, Quaternion rotation)
        {
            PlayerExplorationMovement move = Hero().GetComponent<PlayerExplorationMovement>();
            if (move != null) move.TeleportTo(position, rotation);
            else Hero().transform.position = position;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private static IEnumerator Shot(Camera cam, string file)
        {
            yield return null;
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

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }
    }
}
