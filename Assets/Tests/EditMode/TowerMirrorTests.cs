using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using CastleOfTheD20.Editor;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Treasure Tower: the way back to the Castle Hall is the top of the spiral stairs, and a hidden mirror room
    /// waits behind the north wall, sealed in the saved scene until the Tower challenge opens it.
    /// </summary>
    [TestFixture]
    public class TowerMirrorTests
    {
        [SetUp]
        public void OpenTower()
        {
            EditorSceneManager.OpenScene(TowerMirrorSetup.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void LeaveEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void SouthGate_IsTheTopOfTheSpiralStairs()
        {
            Transform gate = GameObject.Find(TowerMirrorSetup.GateName).transform;
            Transform stairs = gate.Find(HallDoorsSetup.StairsModelName);
            Assert.IsNotNull(stairs, "the stairs' top end stands in the gate");
            Assert.IsNull(gate.GetComponentInChildren<SwingDoor>(true), "no wooden door left in the gate");

            DoorTeleporter door = gate.GetComponentInChildren<DoorTeleporter>(true);
            Assert.AreEqual("Zone_5_CastleHall", door.TargetSceneName);
            Assert.AreEqual("Spawn_From_Tower", door.TargetSpawnPointName);
            Assert.IsTrue(door.TriggerOnWalk, "stepping onto the landing is enough");

            int tops = 0;
            bool anchor = false;
            foreach (Transform t in stairs.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(ZoneExitSign.PillarTopPrefix)) tops++;
                if (t.name == ZoneExitSign.SignAnchorName) anchor = true;
            }
            Assert.AreEqual(2, tops, "two buttress tops for the braziers");
            Assert.IsTrue(anchor, "the sign hangs above the arch");
        }

        [Test]
        public void HeroWalksFromSpawnStraightIntoTheStairs()
        {
            Transform spawn = GameObject.Find("Spawn_From_Hall").transform;
            Transform gate = GameObject.Find(TowerMirrorSetup.GateName).transform;
            BoxCollider trigger = gate.GetComponentInChildren<DoorTeleporter>(true).GetComponent<BoxCollider>();
            Assert.IsFalse(trigger.bounds.Contains(spawn.position), "arriving from the hall does not land in the trigger");

            Vector3 start = new Vector3(spawn.position.x, 0f, spawn.position.z);
            Vector3 dir = gate.forward;
            float toTrigger = Vector3.Dot(trigger.bounds.center - start, dir);
            Vector3 bottom = start + Vector3.up * 0.6f, top = start + Vector3.up * 1.5f;
            bool blocked = Physics.CapsuleCast(bottom, top, 0.4f, dir, out RaycastHit hit, toTrigger, ~0, QueryTriggerInteraction.Ignore);
            Assert.IsFalse(blocked, blocked ? $"walk blocked by {hit.collider.name}" : string.Empty);
            Assert.IsTrue(trigger.bounds.Contains(start + dir * toTrigger + Vector3.up * 1f), "the walk ends inside the trigger");
        }

        [Test]
        public void MirrorRoom_IsSealedBehindTheNorthWall()
        {
            TowerMirrorRoom opener = Object.FindAnyObjectByType<TowerMirrorRoom>();
            Assert.IsNotNull(opener, "the mirror room's opener is in the scene");
            Assert.IsNotNull(opener.Room);
            Assert.IsFalse(opener.Room.activeSelf, "the room is hidden until the challenge is done");
            Assert.AreEqual("Tower_Wall_North", opener.BlockingWall != null ? opener.BlockingWall.name : null);
            Assert.AreEqual(2, opener.GoldHoard.Length);
            Assert.IsNotNull(opener.GoldHoard[0], "the gold heaps");
            Assert.AreEqual("Gold_Piles", opener.GoldHoard[0].name);

            PlanarMirror mirror = opener.Room.GetComponentInChildren<PlanarMirror>(true);
            Assert.IsNotNull(mirror, "the great mirror");
            Assert.Greater(mirror.transform.position.z, TowerMirrorSetup.RoomNorth - 1f, "it stands at the north wall");
            Assert.Less(Mathf.Abs(mirror.transform.position.x), 0.5f, "in the middle");
            Assert.Greater(Vector3.Dot(mirror.WorldNormal, Vector3.back), 0.99f, "faces south, into the room");
            Assert.AreEqual("CastleOfDice/PlanarMirror", mirror.GetComponent<Renderer>().sharedMaterial.shader.name);
        }

        [Test]
        public void MirrorRoom_WallsCarryEvenStoneNeverStretched()
        {
            TowerMirrorRoom opener = Object.FindAnyObjectByType<TowerMirrorRoom>();
            int walls = 0;
            foreach (MeshFilter mf in opener.Room.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.StartsWith("Room_Wall_")) continue;
                walls++;
                Mesh mesh = mf.sharedMesh;
                Vector3[] v = mesh.vertices;
                Vector2[] uv = mesh.uv;
                int[] tris = mesh.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = mf.transform.TransformPoint(v[tris[i]]), b = mf.transform.TransformPoint(v[tris[i + 1]]);
                    float world = (a - b).magnitude;
                    float tex = (uv[tris[i]] - uv[tris[i + 1]]).magnitude * TowerMirrorSetup.TileMetres;
                    Assert.AreEqual(world, tex, 0.01f, $"{mf.name}: one texture repeat per {TowerMirrorSetup.TileMetres} m");
                }
            }
            Assert.AreEqual(5, walls, "west, east, north and two south walls");
        }
    }
}
