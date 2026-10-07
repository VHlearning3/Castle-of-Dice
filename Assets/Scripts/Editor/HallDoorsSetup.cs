using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Castle Hall exits (2026-10-07):
    /// <list type="bullet">
    /// <item>The Tower gate's wooden double door gives way to the foot of a stone spiral staircase
    /// (BlenderSources/HallTowerStairs_build.py → Assets/Models/Doors/Hall_Tower_Stairs.fbx): an arch in the
    /// east wall, a round stair turret behind it and steps winding up out of sight. Stepping onto the first
    /// steps takes the hero to the Tower through the gate's existing DoorTeleporter.</item>
    /// <item>The Throne Room double door turns black and gloomy: blackened wood, dark iron, a soot-dark arch
    /// and a dim red glow at its foot. It keeps its SwingDoor.</item>
    /// </list>
    /// Edits only those two gate groups in Zone_5_CastleHall; running it again rebuilds the same objects.
    /// </summary>
    public static class HallDoorsSetup
    {
        public const string StairsModelPath = CastleDoorSetup.ModelFolder + "/Hall_Tower_Stairs.fbx";
        public const string StairsPrefabPath = CastleDoorSetup.PrefabFolder + "/Hall_Tower_Stairs.prefab";
        public const string ScenePath = "Assets/Scenes/Zone_5_CastleHall.unity";
        public const string TowerGateName = "Door_Hall_Tower";
        public const string ThroneGateName = "Door_Hall_ThroneRoom";
        public const string StairsModelName = "Stairs_Model";
        public const string GloomLightName = "Gloom_Light";

        [MenuItem("CastleOfDice/Castle Hall Stairs + Gloomy Throne Door", false, 16)]
        public static void SetupMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupAll();
        }

        public static void BatchSetup()
        {
            SetupAll();
            EditorApplication.Exit(0);
        }

        public static void SetupAll()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform tower = FindRoot(scene, TowerGateName);
            Transform throne = FindRoot(scene, ThroneGateName);
            if (tower == null || throne == null)
            {
                Debug.LogError("[HallDoorsSetup] Castle Hall gate groups not found.");
                return;
            }

            Material wallMat = null;
            Transform fill = tower.Find(CastleDoorSetup.WallFillName + "/Fill_L");
            if (fill != null) wallMat = fill.GetComponent<Renderer>().sharedMaterial;

            BuildStairsPrefab(wallMat);
            PlaceStairs(tower);
            MakeThroneDoorGloomy(throne);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[HallDoorsSetup] Castle Hall: Tower stairs placed, Throne Room door darkened.");
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name == name) return r.transform;
            }
            return null;
        }

        #region Tower stairs

        private static void BuildStairsPrefab(Material wallMat)
        {
            // The hall's even stone (HallWallsSetup) on the wall piece and the turret, once it exists
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(HallWallsSetup.MaterialPath);
            if (stone != null) wallMat = stone;
            var mats = new Dictionary<string, Material>
            {
                { "Door_Stone", Mat("M_Door_Stone") },
                { "Door_StoneTrim", Mat("M_Door_StoneTrim") },
                { "Door_Iron", Mat("M_Door_Iron") },
                { "Door_Passage", Mat("M_Door_Passage") },
                { "Stair_Stone", Lit("M_Stair_Stone", new Color(0.55f, 0.52f, 0.48f), 0.1f) },
                { "Stair_Tower", stone != null ? stone : Lit("M_Stair_Tower", new Color(0.50f, 0.47f, 0.43f), 0.08f) },
                { "Stair_Roof", Lit("M_Stair_Roof", new Color(0.17f, 0.19f, 0.25f), 0.25f) },
                { "Hall_Wall", wallMat != null ? wallMat : Lit("M_Stair_Tower", new Color(0.50f, 0.47f, 0.43f), 0.08f) },
            };

            var importer = AssetImporter.GetAtPath(StairsModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[HallDoorsSetup] No model at {StairsModelPath}.");
                return;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var pair in mats)
            {
                if (pair.Value != null) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            }
            importer.SaveAndReimport();

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(StairsModelPath);
            var root = new GameObject("Hall_Tower_Stairs");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);

            // Turn the model so its hall side faces -Z (the gate's front, like the castle doors)
            Transform hallSide = Find(inst.transform, "Hall_Side");
            if (hallSide != null)
            {
                Vector3 toHall = hallSide.position - root.transform.position;
                toHall.y = 0f;
                float yaw = Mathf.Round(Vector3.SignedAngle(toHall, Vector3.back, Vector3.up) / 90f) * 90f;
                inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }

            // Stone that blocks the hero: the wall with its arch and the turret. The steps stay walk-through;
            // the gate's trigger on the first steps sends the hero up to the Tower.
            foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name.EndsWith("_Facade") || mf.name.EndsWith("_Turret"))
                {
                    mf.gameObject.layer = 7; // Obstacle
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }

            // A torch-warm light inside the turret so the steps read in the dark opening
            var lightGo = new GameObject("Stair_Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0.6f, 3.2f, 2.6f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.64f, 0.32f);
            light.range = 7f;
            light.intensity = 2.4f;
            light.shadows = LightShadows.None;

            PrefabUtility.SaveAsPrefabAsset(root, StairsPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void PlaceStairs(Transform gate)
        {
            foreach (string old in new[] { CastleDoorSetup.DoorModelName, StairsModelName })
            {
                Transform t;
                while ((t = gate.Find(old)) != null) Object.DestroyImmediate(t.gameObject);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StairsPrefabPath);
            var stairs = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gate);
            stairs.name = StairsModelName;
            stairs.transform.localPosition = Vector3.zero;
            stairs.transform.localRotation = Quaternion.identity;
            stairs.transform.localScale = Vector3.one;

            // The trigger sits on the first steps just inside the arch
            DoorTeleporter door = gate.GetComponentInChildren<DoorTeleporter>(true);
            if (door != null)
            {
                door.transform.localPosition = new Vector3(0f, 1.5f, 1.3f);
                var box = door.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = Vector3.zero;
                    box.size = new Vector3(3.2f, 3f, 2.4f);
                    EditorUtility.SetDirty(box);
                }
                EditorUtility.SetDirty(door.transform);
            }
        }

        #endregion

        #region Throne Room door

        private static void MakeThroneDoorGloomy(Transform gate)
        {
            Transform model = gate.Find(CastleDoorSetup.DoorModelName);
            if (model == null)
            {
                Debug.LogError("[HallDoorsSetup] The Throne Room gate has no door model.");
                return;
            }

            var gloom = new Dictionary<string, Material>
            {
                { "M_Door_Stone", Lit("M_Door_Stone_Gloom", new Color(0.21f, 0.20f, 0.21f), 0.08f) },
                { "M_Door_StoneTrim", Lit("M_Door_StoneTrim_Gloom", new Color(0.13f, 0.12f, 0.13f), 0.08f) },
                { "M_Door_Wood", Lit("M_Door_Wood_Gloom", new Color(0.075f, 0.055f, 0.05f), 0.15f) },
                { "M_Door_WoodDark", Lit("M_Door_WoodDark_Gloom", new Color(0.035f, 0.028f, 0.028f), 0.1f) },
                { "M_Door_Iron", Lit("M_Door_Iron_Gloom", new Color(0.05f, 0.05f, 0.06f), 0.55f, 0.75f) },
            };
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string key = mats[i].name.Replace("_Gloom", string.Empty);
                    if (gloom.TryGetValue(key, out Material dark) && mats[i] != dark)
                    {
                        mats[i] = dark;
                        changed = true;
                    }
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
            }

            // A dim red glow seeping out at the foot of the door
            Transform old = gate.Find(GloomLightName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var lightGo = new GameObject(GloomLightName);
            lightGo.transform.SetParent(gate, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.8f, -1.2f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.6f, 0.12f, 0.16f);
            light.range = 5.5f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
        }

        #endregion

        private static Material Mat(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(CastleDoorSetup.MaterialFolder + "/" + name + ".mat");
        }

        private static Material Lit(string name, Color color, float smoothness, float metallic = 0f)
        {
            string path = CastleDoorSetup.MaterialFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }
    }
}
