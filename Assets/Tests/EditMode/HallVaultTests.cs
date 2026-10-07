using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using CastleOfTheD20.Editor;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// The Queen's vault stands in the Castle Hall's south-east corner behind an iron door that swings on its
    /// hinge, the reward chest waits inside, and Sir Aldric is a ghost knight, not a skeleton.
    /// </summary>
    [TestFixture]
    public class HallVaultTests
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
        public void Vault_StandsInTheCornerWithAnIronDoor()
        {
            HallVault vault = Object.FindAnyObjectByType<HallVault>(FindObjectsInactive.Include);
            Assert.IsNotNull(vault);
            Transform structure = vault.transform.Find(HallVaultSetup.StructureName);
            Assert.IsNotNull(structure, "the Blender strongroom");
            Bounds b = Bounds(structure);
            Assert.Greater(b.max.x, 38.5f, "against the east wall");
            Assert.Less(b.min.z, -38.5f, "against the south wall");
            Assert.Less(b.size.x, 11f, "a room in the corner, not a wall-long block");

            Assert.IsNotNull(vault.DoorLeaf, "an iron leaf that swings");
            Assert.IsNull(vault.DoorLeaf.GetComponent<Collider>(), "the leaf itself does not block; the blocker does");
            Assert.IsNotNull(vault.Door.GetComponent<BoxCollider>(), "the doorway is blocked while shut");
            Assert.AreEqual(100f, Mathf.Abs(vault.OpenAngle), 0.01f);

            // Swung open, the leaf moves into the vault (south), clear of the doorway
            Vector3 closed = vault.DoorLeaf.GetComponent<Renderer>().bounds.center;
            Quaternion rest = vault.DoorLeaf.localRotation;
            vault.DoorLeaf.localRotation = rest * Quaternion.Euler(0f, vault.OpenAngle, 0f);
            Vector3 open = vault.DoorLeaf.GetComponent<Renderer>().bounds.center;
            vault.DoorLeaf.localRotation = rest;
            Assert.Less(open.z, closed.z - 0.8f, "the door swings inward");

            Assert.IsNotNull(vault.RewardChest);
            Assert.IsTrue(b.Contains(vault.RewardChest.transform.position + Vector3.up * 0.3f), "the chest waits inside the vault");
            Assert.IsNull(GameObject.Find("Walls/Hall_Pillar_3"), "the old corner pillar made way");
        }

        [Test]
        public void Vault_WallsBlockButTheDoorwayLeadsIn()
        {
            HallVault vault = Object.FindAnyObjectByType<HallVault>(FindObjectsInactive.Include);
            Vector3 chest = vault.RewardChest.transform.position;
            Vector3 doorway = vault.Door.transform.position;
            doorway.y = 1f;

            // Straight through the west wall: stone in the way
            Vector3 west = new Vector3(chest.x - 8f, 1f, chest.z);
            Assert.IsTrue(Physics.Raycast(west, Vector3.right, 8f, 1 << 7), "the vault's west wall blocks");

            // With the door blocker gone, the way from the hall through the doorway to the chest is clear
            vault.Door.SetActive(false);
            Physics.SyncTransforms();
            Vector3 outside = doorway + new Vector3(0f, 0f, 3f);
            Vector3 inside = new Vector3(chest.x, 1f, chest.z + 1.5f);
            Assert.IsFalse(Physics.Linecast(outside, doorway + new Vector3(0f, 0f, -1.5f), 1 << 7), "through the doorway");
            Assert.IsFalse(Physics.Linecast(doorway + new Vector3(0f, 0f, -1.5f), inside, 1 << 7), "on to the chest");
        }

        [Test]
        public void Aldric_IsAGhostKnight()
        {
            GameObject ghost = GameObject.Find(HallVaultSetup.GhostName);
            Assert.IsNotNull(ghost);
            Assert.IsNotNull(ghost.GetComponent<ScriptedNpc>(), "he still talks (the riddle)");
            Transform visual = ghost.transform.Find("Visuals");
            Assert.IsNotNull(visual.GetComponent<GhostFloat>(), "he drifts and his wisp sways");
            Assert.IsNull(visual.GetComponentInChildren<Animator>(true), "no skeleton rig");
            bool tail = false, eyes = false;
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
            {
                StringAssert.DoesNotContain("Skeleton", r.name);
                if (r.name == "Ghost_Tail_3") tail = true;
                if (r.name == "Ghost_Eyes") eyes = true;
                foreach (Material m in r.sharedMaterials) StringAssert.StartsWith("M_Ghost_", m.name, r.name);
            }
            Assert.IsTrue(tail && eyes, "a wisp instead of legs, and glowing eyes");
            Assert.Less(Vector3.Distance(ghost.transform.position, HallVaultSetup.GhostFloorPosition + Vector3.up * 1.4f), 0.01f, "in front of the vault door");
        }

        private static Bounds Bounds(Transform root)
        {
            Renderer[] rs = root.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
