using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using CastleOfTheD20.Editor;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// Castle Hall exits: the Tower gate is the foot of a spiral staircase whose first steps take the hero
    /// to the Tower, and the Throne Room door is black and gloomy but still swings open.
    /// </summary>
    [TestFixture]
    public class HallDoorsTests
    {
        [SetUp]
        public void OpenHall()
        {
            EditorSceneManager.OpenScene(HallDoorsSetup.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void LeaveEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void TowerGate_IsSpiralStairsLeadingToTheTower()
        {
            Transform gate = GameObject.Find(HallDoorsSetup.TowerGateName).transform;
            Assert.IsNotNull(gate.Find(HallDoorsSetup.StairsModelName), "the stairs stand in the Tower gate");
            Assert.IsNull(gate.GetComponentInChildren<SwingDoor>(true), "no wooden door left in front of the stairs");

            DoorTeleporter door = gate.GetComponentInChildren<DoorTeleporter>(true);
            Assert.AreEqual("Zone_6_Tower", door.TargetSceneName);
            Assert.AreEqual("Tower", door.DestinationZone);
            Assert.IsTrue(door.TriggerOnWalk, "stepping onto the stairs is enough");
        }

        [Test]
        public void TowerStairs_HeroWalksFromSpawnStraightIntoTheTrigger()
        {
            Transform spawn = GameObject.Find("Spawn_From_Tower").transform;
            Transform gate = GameObject.Find(HallDoorsSetup.TowerGateName).transform;
            BoxCollider trigger = gate.GetComponentInChildren<DoorTeleporter>(true).GetComponent<BoxCollider>();

            Assert.IsFalse(trigger.bounds.Contains(spawn.position), "a hero coming back down does not land in the trigger");

            // A hero-sized capsule walking from the spawn toward the stairs meets no stone before the trigger
            // (it starts 0.2 m up: the hero steps over the 0.1 m sill)
            Vector3 start = new Vector3(spawn.position.x, 0f, spawn.position.z);
            Vector3 dir = gate.forward;
            float toTrigger = Vector3.Dot(trigger.bounds.center - start, dir);
            Vector3 bottom = start + Vector3.up * 0.6f, top = start + Vector3.up * 1.5f;
            bool blocked = Physics.CapsuleCast(bottom, top, 0.4f, dir, out RaycastHit hit, toTrigger, 1 << 7, QueryTriggerInteraction.Ignore);
            Assert.IsFalse(blocked, blocked ? $"walk blocked by {hit.collider.name}" : string.Empty);
            Assert.IsTrue(trigger.bounds.Contains(start + dir * toTrigger + Vector3.up * 1f), "the walk ends inside the trigger");
        }

        [Test]
        public void TowerStairs_SignAndBraziersHaveTheirMarks()
        {
            Transform stairs = GameObject.Find(HallDoorsSetup.TowerGateName).transform.Find(HallDoorsSetup.StairsModelName);
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
        public void ThroneRoomDoor_IsDarkAndStillSwings()
        {
            Transform gate = GameObject.Find(HallDoorsSetup.ThroneGateName).transform;
            SwingDoor swing = gate.GetComponentInChildren<SwingDoor>(true);
            Assert.IsNotNull(swing);
            Assert.AreEqual(2, swing.Leaves.Length);
            foreach (Renderer r in swing.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.EndsWith("_Passage")) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    StringAssert.EndsWith("_Gloom", m.name, r.name);
                    Color c = m.GetColor("_BaseColor");
                    Assert.Less(c.maxColorComponent, 0.25f, $"{r.name}/{m.name} is dark");
                }
            }
            Assert.IsNotNull(gate.Find(HallDoorsSetup.GloomLightName));
        }
    }
}
