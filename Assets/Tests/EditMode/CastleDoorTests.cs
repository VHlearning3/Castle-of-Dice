using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CastleOfTheD20.Editor;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Tests
{
    /// <summary>
    /// The wooden castle doors (double and single) and the Hidden Trail (Zone 2.5): the leaves swing on
    /// their outer hinges into the passage, every castle zone exit carries the double door, and the
    /// Forest gate, the trail and the Library's secret side door are wired to each other.
    /// </summary>
    [TestFixture]
    public class CastleDoorTests
    {
        [TearDown]
        public void LeaveEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TestCase(CastleDoorSetup.DoublePrefabPath, 2)]
        [TestCase(CastleDoorSetup.SinglePrefabPath, 1)]
        public void DoorPrefab_LeavesSwingOnOuterHingesIntoThePassage(string path, int leafCount)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                SwingDoor swing = door.GetComponent<SwingDoor>();
                Assert.IsNotNull(swing);
                Assert.AreEqual(leafCount, swing.Leaves.Length);
                Assert.AreEqual(2, door.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith(ZoneExitSign.PillarTopPrefix)), "brazier marks on both pillars");

                var closedCentres = swing.Leaves.Select(l => l.GetComponent<Renderer>().bounds.center).ToArray();
                swing.SetOpenImmediate(true);
                for (int i = 0; i < swing.Leaves.Length; i++)
                {
                    Transform leaf = swing.Leaves[i];
                    Vector3 hingeLocal = door.transform.InverseTransformPoint(leaf.position);
                    Vector3 centreLocal = door.transform.InverseTransformPoint(closedCentres[i]);
                    Assert.Greater(Mathf.Abs(hingeLocal.x), Mathf.Abs(centreLocal.x), $"{leaf.name}: the hinge is on the outer edge");

                    Vector3 openCentre = door.transform.InverseTransformPoint(leaf.GetComponent<Renderer>().bounds.center);
                    Assert.Greater(openCentre.z, centreLocal.z + 0.4f, $"{leaf.name}: swings into the passage (+Z)");
                    Assert.AreEqual(swing.OpenAngle, Mathf.Abs(swing.LeafAngle(i)), 0.01f);
                }
                if (leafCount == 2)
                {
                    Assert.AreEqual(-Mathf.Sign(swing.LeafAngle(0)), Mathf.Sign(swing.LeafAngle(1)), "the two leaves turn opposite ways");
                }

                swing.SetOpenImmediate(false);
                for (int i = 0; i < swing.Leaves.Length; i++)
                {
                    Assert.Less(Vector3.Distance(swing.Leaves[i].GetComponent<Renderer>().bounds.center, closedCentres[i]), 0.001f, "shuts again");
                }
            }
            finally
            {
                Object.DestroyImmediate(door);
            }
        }

        [Test]
        public void CastleZoneExits_AllCarryTheDoubleDoor()
        {
            foreach (string scene in CastleDoorSetup.CastleScenes)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single);
                int exits = 0;
                foreach (DoorTeleporter door in Object.FindObjectsByType<DoorTeleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (string.IsNullOrEmpty(door.TargetSceneName)) continue;
                    Transform group = door.transform.parent;
                    Assert.IsNotNull(group, $"{scene}: {door.name} sits in a gate group");
                    if (group.Find(HallDoorsSetup.StairsModelName) != null) { exits++; continue; } // the Tower spiral stairs (Hall and Tower ends)
                    SwingDoor swing = group.GetComponentInChildren<SwingDoor>();
                    Assert.IsNotNull(swing, $"{scene}: {group.name} has a wooden door");
                    int expected = SecretForestPath.IsScene(door.TargetSceneName) ? 1 : 2;
                    Assert.AreEqual(expected, swing.Leaves.Length, $"{scene}: {group.name}");
                    Assert.IsNull(group.Find("Arch_Model"), $"{scene}: {group.name} lost the old arch");
                    exits++;
                }
                Assert.Greater(exits, 0, scene);
            }
        }

        [Test]
        public void HiddenTrail_IsWiredBetweenForestGateAndLibrarySideDoor()
        {
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == SecretForestPathBuilder.ScenePath), "the trail is in the build");

            EditorSceneManager.OpenScene(SecretForestPathBuilder.ForestScenePath, OpenSceneMode.Single);
            LockpickInteraction gate = GameObject.Find("Locked_Secret_Gate").GetComponent<LockpickInteraction>();
            var hidden = new SerializedObject(gate).FindProperty("hiddenPathObject").objectReferenceValue as GameObject;
            Assert.IsNotNull(hidden, "the gate reveals a door");
            DoorTeleporter forestDoor = hidden.GetComponentInChildren<DoorTeleporter>(true);
            Assert.AreEqual(SecretForestPath.SceneName, forestDoor.TargetSceneName, "behind the gate lies the trail");
            Assert.AreEqual(SecretForestPath.SpawnFromForest, forestDoor.TargetSpawnPointName);
            Assert.IsNotNull(GameObject.Find(SecretForestPath.ForestSpawnFromTrail), "landing spot in the Forest for the way back");

            EditorSceneManager.OpenScene(SecretForestPathBuilder.ScenePath, OpenSceneMode.Single);
            Assert.IsNotNull(GameObject.Find(SecretForestPath.SpawnFromForest));
            Assert.IsNotNull(GameObject.Find(SecretForestPath.SpawnFromLibrary));
            var doors = Object.FindObjectsByType<DoorTeleporter>(FindObjectsSortMode.None);
            DoorTeleporter back = doors.Single(d => d.TargetSceneName == "Zone_2_ForestPath");
            Assert.AreEqual(SecretForestPath.ForestSpawnFromTrail, back.TargetSpawnPointName);
            DoorTeleporter toLibrary = doors.Single(d => d.TargetSceneName == "Zone_4_Library");
            Assert.AreEqual(SecretForestPath.LibrarySpawnFromTrail, toLibrary.TargetSpawnPointName);
            Assert.AreEqual(1, toLibrary.transform.parent.GetComponentInChildren<SwingDoor>().Leaves.Length, "the trail ends at the single door");
            Assert.AreEqual(0, Object.FindObjectsByType<DungeonRoomController>(FindObjectsSortMode.None).Length, "no fights on the trail");

            EditorSceneManager.OpenScene("Assets/Scenes/Zone_4_Library.unity", OpenSceneMode.Single);
            Assert.IsNotNull(GameObject.Find(SecretForestPath.LibrarySpawnFromTrail), "the side door has its own landing spot");
            DoorTeleporter side = Object.FindObjectsByType<DoorTeleporter>(FindObjectsSortMode.None).Single(d => SecretForestPath.IsScene(d.TargetSceneName));
            Assert.AreEqual(SecretForestPath.SpawnFromLibrary, side.TargetSpawnPointName);
            Assert.AreEqual(CastleDoorSetup.LibrarySecretDoorName, side.transform.parent.name);
            Assert.IsFalse(Object.FindObjectsByType<DoorTeleporter>(FindObjectsSortMode.None).Any(d => d.TargetSceneName == "Zone_2_ForestPath"),
                "the Library no longer jumps straight back to the Forest Path");
        }

        [Test]
        public void ExitSign_NamesTheHiddenTrail()
        {
            var go = new GameObject("Door_Test");
            try
            {
                DoorTeleporter door = go.AddComponent<DoorTeleporter>();
                door.InitializeSceneTeleporter(SecretForestPath.SceneName, SecretForestPath.SpawnFromForest, "Forest", "Follow the hidden trail");
                Assert.AreEqual(SecretForestPath.DisplayName, ZoneExitSign.DestinationName(door));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
