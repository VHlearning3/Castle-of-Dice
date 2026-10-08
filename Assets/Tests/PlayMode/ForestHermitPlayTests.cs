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
    /// Vili, 2026-10-07: a long-bearded old man sits on the log between the pond and the cottage in the Forest
    /// Path and always says "Stay a while and listen." when the hero talks to him: no dialogue panel, no other
    /// lines. The zone's walls became tree lines and hills that still close the borders.
    /// Set FOREST_SHOTS_DIR to save review screenshots.
    /// </summary>
    public class ForestHermitPlayTests
    {
        private const string SaveKey = "CastleOfDice_SaveData";
        private string saveBackup;
        private PlayerUnit hero;
        private CharacterController cc;

        [SetUp]
        public void BackupSave() => saveBackup = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;

        [TearDown]
        public void RestoreSave()
        {
            Time.timeScale = 1f;
            if (saveBackup != null) PlayerPrefs.SetString(SaveKey, saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Hermit_SitsOnTheLog_AndSaysHisLineEveryTime()
        {
            yield return EnterForestPath();
            ForestHermit hermit = Object.FindAnyObjectByType<ForestHermit>();
            Assert.IsNotNull(hermit, "The old hermit is in the Forest Path.");
            Animator anim = hermit.GetComponentInChildren<Animator>();
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int i = 0; i < 5; i++) yield return null;

            // Seated on the log: hips just above its top, feet on the ground
            Transform log = GameObject.Find("East_Cottage").transform.Find("Log");
            float logTop = Bounds(log.gameObject).max.y;
            float hips = anim.GetBoneTransform(HumanBodyBones.Hips).position.y;
            Assert.That(hips - logTop, Is.InRange(0.02f, 0.4f), $"Hips at {hips:F2} sit on the log top {logTop:F2}.");
            float foot = Mathf.Min(anim.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, anim.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
            Assert.That(foot, Is.InRange(-0.05f, 0.35f), $"Feet ({foot:F2}) rest on the ground.");

            int dialogues = 0;
            void Count(DialogueNodeSO _) => dialogues++;
            DialogueController.OnDialogueStarted += Count;
            try
            {
                cc.enabled = false;
                hero.transform.position = hermit.transform.position + new Vector3(2.3f, 0.5f, -1.9f);
                cc.enabled = true;
                yield return null;
                Assert.IsTrue(hermit.CanInteract(hero), "The hero standing in front of him can talk to him.");

                for (int round = 0; round < 2; round++)
                {
                    hermit.TriggerInteraction(hero);
                    yield return null;
                    Assert.IsNotNull(hermit.Bubble, "Talking shows a speech bubble.");
                    Assert.IsTrue(hermit.Bubble.IsShowing, "The bubble is up.");
                    Assert.AreEqual("Stay a while and listen.", hermit.Bubble.Text);
                    Assert.Greater(hermit.Bubble.transform.position.y, hips + 1.2f, "The bubble floats above his head.");
                    Assert.AreEqual(0, dialogues, "No dialogue panel opens.");
                    if (GameManager.Instance != null) Assert.AreEqual(GamePlayMode.Exploration, GameManager.Instance.CurrentMode);
                    // let the bubble finish popping up and the hand come up (batch-mode frames are very short)
                    float shownAt = Time.time;
                    while (Time.time - shownAt < 0.6f) yield return null;
                    Vector3 at = hermit.transform.position;
                    if (round == 0) yield return Shots("hermit_bubble", (at + new Vector3(0f, 12.5f, -12.5f), at + Vector3.up), (at + new Vector3(-2.4f, 2.6f, -7.5f), at + Vector3.up * 2.4f));
                    for (int i = 0; i < 6; i++) yield return null;
                    AnimatorStateInfo st = anim.IsInTransition(0) ? anim.GetNextAnimatorStateInfo(0) : anim.GetCurrentAnimatorStateInfo(0);
                    Assert.IsTrue(st.IsName(ForestHermit.SpeakState), "He gestures while he speaks.");

                    float until = Time.realtimeSinceStartup + 6f;
                    while (hermit.Bubble.IsShowing && Time.realtimeSinceStartup < until) yield return null;
                    Assert.IsFalse(hermit.Bubble.IsShowing, "The bubble goes away after a few seconds.");
                }
            }
            finally
            {
                DialogueController.OnDialogueStarted -= Count;
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Borders_AreTreesAndHills_AndStillClosed()
        {
            yield return EnterForestPath();
            foreach (string wall in new[] { "Forest_Wall_East", "Forest_Wall_South_L", "Forest_Wall_South_R", "Forest_Wall_North_L", "Forest_Wall_North_R", "Forest_Wall_West_S", "Forest_Wall_West_N" })
            {
                GameObject w = GameObject.Find(wall);
                Assert.IsNotNull(w, wall + " still marks the border.");
                Assert.IsFalse(w.GetComponent<MeshRenderer>().enabled, wall + " is no longer drawn.");
                Assert.IsTrue(w.GetComponent<BoxCollider>().enabled, wall + " still stops the hero.");
            }
            Transform border = GameObject.Find("Forest_Border").transform;
            Assert.Greater(border.Find("Tree_Lines").childCount, 150, "Dense tree lines along the borders.");
            Assert.AreEqual(4, border.Find("Hills").childCount, "Hills rise behind the tree lines.");

            // The hero walking east into the old wall line is stopped before it
            cc.enabled = false;
            hero.transform.position = new Vector3(74f, 0.5f, -40f);
            cc.enabled = true;
            yield return null;
            for (int i = 0; i < 200; i++)
            {
                cc.Move(Vector3.right * 0.1f + Vector3.down * 0.05f);
                yield return null;
            }
            Assert.Less(hero.transform.position.x, 79.5f, "The east border still holds.");

            // Ground and roads wear the village textures
            Assert.AreEqual("M_Village_Grass", GameObject.Find("Forest_Ground").GetComponent<MeshRenderer>().sharedMaterial.name);
            Assert.AreEqual("M_Village_Dirt", GameObject.Find("Forest_Path_Main").GetComponent<MeshRenderer>().sharedMaterial.name);

            yield return Shots("border_east", (new Vector3(58f, 13f, -30f), new Vector3(84f, 1f, -22f)), (new Vector3(66f, 4f, -45f), new Vector3(90f, 3f, -35f)));
            yield return Shots("border_north", (new Vector3(30f, 13f, 28f), new Vector3(34f, 1f, 58f)), (new Vector3(-20f, 13f, 28f), new Vector3(-24f, 1f, 58f)));
            yield return Shots("forest_path", (new Vector3(0f, 12.5f, -42.5f), new Vector3(0f, 0.8f, -30f)), (new Vector3(-30f, 12f, -60f), new Vector3(-48f, 1f, -66f)));
        }

        private IEnumerator Shots(string name, (Vector3 pos, Vector3 look) game, (Vector3 pos, Vector3 look) close)
        {
            string dir = System.Environment.GetEnvironmentVariable("FOREST_SHOTS_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            Directory.CreateDirectory(dir);
            Camera cam = Camera.main;
            CameraFollow follow = cam.GetComponent<CameraFollow>();
            if (follow != null) follow.enabled = false;
            var views = new[]
            {
                (tag: "game", pos: game.pos, look: game.look),
                (tag: "close", pos: close.pos, look: close.look),
            };
            foreach (var v in views)
            {
                cam.transform.position = v.pos;
                cam.transform.LookAt(v.look);
                yield return null;
                var rt = new RenderTexture(1600, 900, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, name + "_" + v.tag + ".png"), tex.EncodeToPNG());
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.Destroy(rt);
                Object.Destroy(tex);
            }
            if (follow != null) follow.enabled = true;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Fights_StandWhereViliDrewThem()
        {
            yield return EnterForestPath();
            // Z: the zombie west of the road in the south, a watchtower beside it
            Vector3 zombie = Find("Forest_Zombie").position;
            Assert.That(zombie.x, Is.InRange(-11f, -3f), $"The zombie waits west of the road ({zombie}).");
            Assert.That(zombie.z, Is.InRange(-74f, -60f), $"The zombie is in the south part of the woods ({zombie}).");
            Transform tower = Find("Zombie_Watchtower");
            Assert.Less(Vector3.Distance(tower.position, zombie), 11f, "The watchtower stands next to the zombie.");
            Assert.IsNotNull(tower.GetComponent<BoxCollider>(), "The tower blocks the way.");
            Assert.Greater(Bounds(tower.gameObject).max.y, 7f, "The tower rises above the trees' lower branches.");
            Assert.Less(TriggerBounds("Forest_Zombie_Trigger").min.x, -3f, "Walking the road reaches the zombie's trigger.");
            Assert.Greater(TriggerBounds("Forest_Zombie_Trigger").max.x, 3f, "Walking the road reaches the zombie's trigger.");

            // A: the ambush at the crossroads to the lockpick gate
            Bounds ambush = TriggerBounds("Forest_Ambush_Trigger");
            Assert.IsTrue(ambush.min.x < -6f && ambush.max.x > -4f && ambush.min.z < 13f && ambush.max.z > 13f, $"The ambush covers the crossroads ({ambush.center}).");

            // W: the wolves' patrol circle is in the woods east of the road
            WolfPackPatrol patrol = Object.FindAnyObjectByType<WolfPackPatrol>();
            Assert.IsNotNull(patrol, "The wolves patrol.");
            Assert.That(patrol.Center.x, Is.InRange(35f, 60f));
            Assert.That(patrol.Center.z, Is.InRange(-70f, -45f));
            Assert.GreaterOrEqual(patrol.Radius, 10f, "A wide circle.");
            yield return Shots("fight_spots", (new Vector3(-8f, 13f, -84f), new Vector3(-9f, 1f, -68f)), (tower.position + new Vector3(9f, 5f, -10f), tower.position + new Vector3(0f, 4f, 0f)));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Wolves_PatrolACircle_AndAttackWhenTheHeroComesNear()
        {
            yield return EnterForestPath("ForestWolves");
            WolfPackPatrol patrol = Object.FindAnyObjectByType<WolfPackPatrol>();
            DungeonRoomController room = patrol.Room;
            GameObject wolf = room.roomEnemies[0];
            yield return null;
            Assert.IsTrue(wolf.activeInHierarchy, "The wolves are out in the woods before any fight.");
            Vector3 a = wolf.transform.position;
            float t0 = Time.time;
            while (Time.time - t0 < 2f) yield return null;
            Vector3 b = wolf.transform.position;
            Assert.Greater(Vector3.Distance(a, b), 1.5f, "The wolf walks.");
            float r = Vector2.Distance(new Vector2(b.x, b.z), new Vector2(patrol.Center.x, patrol.Center.z));
            Assert.That(r, Is.EqualTo(patrol.Radius).Within(0.3f), "...along its circle.");
            Assert.AreEqual(RoomState.Unexplored, room.CurrentState, "No fight while the hero is far away.");
            Animator anim = wolf.GetComponentInChildren<Animator>();
            Assert.IsTrue(anim.GetBool("IsMoving"), "The wolf plays its walk.");
            yield return Shots("wolves_patrol", (b + new Vector3(0f, 12.5f, -12.5f), b + Vector3.up), (b + new Vector3(-4f, 3f, -8f), b + Vector3.up));

            // The hero walks up to the pack: the fight starts there
            Vector3 dir = (b - patrol.Center); dir.y = 0f; dir.Normalize();
            cc.enabled = false;
            hero.transform.position = b + dir * 7f + Vector3.up * 0.5f;
            cc.enabled = true;
            float until = Time.realtimeSinceStartup + 5f;
            while (room.CurrentState == RoomState.Unexplored && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(RoomState.CombatActive, room.CurrentState, "Coming near the wolves starts their fight.");
            Assert.IsTrue(TurnManager.Instance != null && TurnManager.Instance.IsCombatActive, "Turn-based combat runs.");
            Assert.Less(Vector3.Distance(room.transform.position, b), 10f, "The fight is laid out where the hero met the pack.");
            for (int i = 0; i < 10; i++) yield return null;
            Vector3 w = wolf.transform.position;
            yield return Shots("wolves_fight", (w + new Vector3(0f, 13f, -13f), w + Vector3.up), (w + new Vector3(-5f, 4f, -9f), w + Vector3.up));
        }

        /// <summary>A room trigger's box in world space, read from its shape (the room may be switched off).</summary>
        private static Bounds TriggerBounds(string name)
        {
            Transform t = Find(name);
            BoxCollider box = t.GetComponent<BoxCollider>();
            return new Bounds(t.TransformPoint(box.center), Vector3.Scale(box.size, t.lossyScale));
        }

        private static Transform Find(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name && t.gameObject.scene == SceneManager.GetActiveScene()) return t;
            Assert.Fail("Missing " + name);
            return null;
        }

        private IEnumerator EnterForestPath(string keepRoomKey = null)
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
            yield return WaitForLoad();
            StoryPanelUI.CloseIfOpen();

            SceneLoader.Instance.LoadScene("Zone_2_ForestPath");
            for (int i = 0; i < 2; i++) yield return null;
            yield return WaitForLoad();
            Assert.AreEqual("Zone_2_ForestPath", SceneManager.GetActiveScene().name);
            StoryPanelUI.CloseIfOpen();

            // Fights would freeze exploration; these tests are about the hermit and the borders
            foreach (DungeonRoomController room in Object.FindObjectsByType<DungeonRoomController>(FindObjectsSortMode.None))
                if (keepRoomKey == null || room.roomKey != keepRoomKey) room.gameObject.SetActive(false);

            hero = Object.FindAnyObjectByType<PlayerUnit>();
            cc = hero != null ? hero.GetComponent<CharacterController>() : null;
            if (cc == null) Assert.Inconclusive("The hero has no CharacterController to walk with.");
            float settle = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < settle) yield return null;
        }

        private static IEnumerator WaitForLoad()
        {
            float until = Time.realtimeSinceStartup + 30f;
            while (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading && Time.realtimeSinceStartup < until) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
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
