using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Zone 2.5, the Hidden Trail: a short forest path behind the Rogue's lockpick gate on the Forest Path.
    /// <list type="bullet">
    /// <item>Creates Assets/Scenes/Zone_2.5_SecretForestPath.unity from the Forest Path's scene skeleton
    /// (camera, light, managers, hero, ground) and lays out its spawns and two doors: a forest arch back to
    /// the Forest Path (south) and the single wooden door into the Library's secret side door (north).</item>
    /// <item>Points the Forest Path's secret door (behind the gate) at the trail and renames its landing spot.</item>
    /// <item>Dresses the trail with <see cref="ZoneDressingBuilder.DressSecretForestPath"/> and adds the scene
    /// to the Build Settings right after the Forest Path.</item>
    /// </list>
    /// No fights here. Running it again rebuilds the trail scene's layout and dressing only.
    /// </summary>
    public static class SecretForestPathBuilder
    {
        public const string ScenePath = "Assets/Scenes/" + SecretForestPath.SceneName + ".unity";
        public const string ForestScenePath = "Assets/Scenes/Zone_2_ForestPath.unity";
        public const string LayoutRootName = "Hidden_Trail_Layout";
        public const string ForestDoorName = "Door_Trail_Forest";
        public const string LibraryDoorName = "Door_Trail_Library";

        /// <summary>Roots of the Forest Path scene the trail keeps (the rest is the Forest's own content).</summary>
        private static readonly HashSet<string> KeepRoots = new HashSet<string>
        {
            "Directional Light", "Main Camera", "EventSystem", "Managers", "PlayerHero", "Forest_Ground",
            "ForestLight_South", "ForestLight_North", LayoutRootName, "Zone_Dressing"
        };

        [MenuItem("CastleOfDice/Build Hidden Trail (Zone 2.5)", false, 16)]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
        }

        /// <summary>Batch-mode entry: door prefabs, trail scene, Forest gate wiring and build settings.</summary>
        public static void BatchBuild()
        {
            BuildAll();
        }

        public static void BuildAll()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CastleDoorSetup.SinglePrefabPath) == null)
            {
                CastleDoorSetup.BuildPrefabs();
            }
            BuildScene();
            ZoneDressingBuilder.DressSecretForestPath();
            WireForestGate();
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[SecretForestPathBuilder] Hidden Trail built and wired to the Forest Path gate and the Library.");
        }

        #region Trail scene

        private static void BuildScene()
        {
            if (!File.Exists(ScenePath))
            {
                AssetDatabase.CopyAsset(ForestScenePath, ScenePath);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (!KeepRoots.Contains(go.name) || go.name == LayoutRootName) Object.DestroyImmediate(go);
            }

            // Ground: a 46 x 66 m clearing around the trail
            GameObject ground = Find(scene, "Forest_Ground");
            if (ground != null)
            {
                ground.transform.position = new Vector3(0f, ground.transform.position.y, -4f);
                Renderer r = ground.GetComponent<Renderer>();
                if (r != null)
                {
                    Vector3 size = r.bounds.size;
                    Vector3 s = ground.transform.localScale;
                    ground.transform.localScale = new Vector3(s.x * 46f / Mathf.Max(size.x, 0.01f), s.y, s.z * 72f / Mathf.Max(size.z, 0.01f));
                }
            }
            Move(scene, "ForestLight_South", new Vector3(0f, 5f, -15f));
            Move(scene, "ForestLight_North", new Vector3(0f, 5f, 12f));

            var layout = new GameObject(LayoutRootName);
            SceneManager.MoveGameObjectToScene(layout, scene);

            // Spawns: just inside the arch from the Forest Path, and in front of the Library door
            Transform fromForest = Spawn(layout.transform, SecretForestPath.SpawnFromForest, new Vector3(0f, 0.5f, -18f), 0f);
            Spawn(layout.transform, SecretForestPath.SpawnFromLibrary, new Vector3(0f, 0.5f, 17.5f), 180f);

            // South: forest arch back to the Forest Path (same arch and pillars as the Forest's own exits)
            ForestArchDoor(layout.transform, ForestDoorName, new Vector3(0f, 0f, -27f), Quaternion.Euler(0f, 180f, 0f),
                "Zone_2_ForestPath", SecretForestPath.ForestSpawnFromTrail, "Forest", "Return to the Whispering Woods");

            // North: the single wooden door in the Library's outer wall
            var group = new GameObject(LibraryDoorName);
            group.transform.SetParent(layout.transform, false);
            group.transform.SetPositionAndRotation(new Vector3(0f, 0f, ZoneDressingBuilder.SecretTrailWallZ), Quaternion.identity);
            var trigger = new GameObject(LibraryDoorName + "_Trigger");
            trigger.transform.SetParent(group.transform, false);
            var col = trigger.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var door = trigger.AddComponent<DoorTeleporter>();
            door.InitializeSceneTeleporter("Zone_4_Library", SecretForestPath.LibrarySpawnFromTrail, "Library",
                "Slip into the Grand Archives (Secret Door)", 4.5f, true);
            GameObject single = AssetDatabase.LoadAssetAtPath<GameObject>(CastleDoorSetup.SinglePrefabPath);
            CastleDoorSetup.PlaceDoor(group.transform, door, single, CastleDoorSetup.SingleDoorScale, 0.8f);
            Transform fill = group.transform.Find(CastleDoorSetup.WallFillName);
            if (fill != null) Object.DestroyImmediate(fill.gameObject);

            // Invisible bounds so the hero stays in the clearing
            var bounds = new GameObject("Trail_Bounds");
            bounds.transform.SetParent(layout.transform, false);
            bounds.layer = 7;
            AddBlock(bounds, new Vector3(-10.5f, 3f, -3f), new Vector3(1f, 6f, 66f));
            AddBlock(bounds, new Vector3(10.5f, 3f, -3f), new Vector3(1f, 6f, 66f));
            AddBlock(bounds, new Vector3(0f, 3f, -31.5f), new Vector3(22f, 6f, 1f));
            AddBlock(bounds, new Vector3(0f, 3f, 26.5f), new Vector3(22f, 6f, 1f));

            // The hero starts at the arch when the scene is played on its own
            GameObject hero = Find(scene, "PlayerHero");
            if (hero != null)
            {
                hero.transform.SetPositionAndRotation(fromForest.position, fromForest.rotation);
            }
            GameObject cam = Find(scene, "Main Camera");
            if (cam != null)
            {
                cam.transform.position = fromForest.position + new Vector3(-6f, 8f, -8f);
                cam.transform.rotation = Quaternion.LookRotation(fromForest.position + Vector3.up - cam.transform.position);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ForestArchDoor(Transform parent, string name, Vector3 pos, Quaternion rot, string targetScene, string spawn, string zone, string prompt)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            group.transform.SetPositionAndRotation(pos, rot);

            GameObject arch = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Arch.prefab");
            GameObject pillar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Pillar.prefab");
            if (arch != null)
            {
                var a = (GameObject)PrefabUtility.InstantiatePrefab(arch, group.transform);
                a.name = "Arch_Model";
                a.transform.localPosition = Vector3.zero;
                a.transform.localRotation = Quaternion.identity;
                a.transform.localScale = Vector3.one * 1.4f;
            }
            if (pillar != null)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var p = (GameObject)PrefabUtility.InstantiatePrefab(pillar, group.transform);
                    p.name = side < 0 ? "DecoPillar_L" : "DecoPillar_R";
                    p.transform.localPosition = new Vector3(side * 4.4f, 0f, 0f);
                }
            }

            var trigger = new GameObject(name + "_Trigger");
            trigger.transform.SetParent(group.transform, false);
            trigger.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var col = trigger.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(8f, 4f, 3f);
            var door = trigger.AddComponent<DoorTeleporter>();
            door.InitializeSceneTeleporter(targetScene, spawn, zone, prompt, 4.5f, true);
        }

        private static Transform Spawn(Transform parent, string name, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<StartSpawnPoint>();
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2f, 2f, 2f);
            return go.transform;
        }

        private static void AddBlock(GameObject owner, Vector3 center, Vector3 size)
        {
            var box = owner.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        #endregion

        #region Forest Path gate

        /// <summary>
        /// The secret door behind the Forest Path's lockpick gate now leads onto the trail (it used to jump
        /// straight into the Library), and the hero coming back from the trail lands just inside the gate.
        /// Who can open the gate is untouched.
        /// </summary>
        private static void WireForestGate()
        {
            Scene scene = EditorSceneManager.OpenScene(ForestScenePath, OpenSceneMode.Single);
            DoorTeleporter secret = null;
            foreach (DoorTeleporter d in Object.FindObjectsByType<DoorTeleporter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform g = d.transform.parent;
                if (g != null && (g.name == "Door_Forest_Library_Secret" || g.name == "Door_Forest_SecretPath")) secret = d;
            }
            if (secret == null)
            {
                Debug.LogError("[SecretForestPathBuilder] The Forest Path's secret door was not found.");
                return;
            }

            secret.transform.parent.name = "Door_Forest_SecretPath";
            secret.name = "Door_Forest_SecretPath_Trigger";
            secret.TargetSceneName = SecretForestPath.SceneName;
            secret.TargetSpawnPointName = SecretForestPath.SpawnFromForest;
            secret.DestinationZone = "Forest";
            var so = new SerializedObject(secret);
            so.FindProperty("promptMessage").stringValue = "Follow the hidden trail";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(secret);

            GameObject oldSpawn = Find(scene, "Spawn_From_Library");
            if (oldSpawn != null) oldSpawn.name = SecretForestPath.ForestSpawnFromTrail;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        #endregion

        #region Build settings

        /// <summary>Adds the trail scene right after the Forest Path (once).</summary>
        public static void RegisterInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            int forest = scenes.FindIndex(s => s.path == ForestScenePath);
            scenes.Insert(forest >= 0 ? forest + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        #endregion

        #region Helpers

        private static GameObject Find(Scene scene, string name)
        {
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name == name) return r;
                foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name) return t.gameObject;
                }
            }
            return null;
        }

        private static void Move(Scene scene, string name, Vector3 pos)
        {
            GameObject go = Find(scene, name);
            if (go != null) go.transform.position = pos;
        }

        #endregion
    }
}
