using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// The Queen's treasure vault and its guard (2026-10-07), both modelled in Blender:
    /// <list type="bullet">
    /// <item>BlenderSources/HallVault_build.py → Assets/Models/Vault/Hall_Vault.fbx: a stone strongroom in the
    /// Castle Hall's south-east corner with an iron door (riveted plates, strap hinges, a lock wheel with a
    /// golden d20) that swings open when Sir Aldric hears the answer to his riddle; gold heaps inside.</item>
    /// <item>BlenderSources/AldricGhost_build.py → Assets/Models/Ghost/Aldric_Ghost.fbx: a spectral knight with
    /// great helm, crest, pauldrons, broken shackles and a sword planted before him, trailing into a wisp
    /// (GhostFloat sways it). Replaces the tinted skeleton.</item>
    /// </list>
    /// Edits Review_Content/Queens_Vault and Ghost_Of_Sir_Aldric in Zone_5_CastleHall in place; ReviewContentBuilder
    /// calls the same helpers, so rebuilding review content keeps them.
    /// </summary>
    public static class HallVaultSetup
    {
        public const string VaultModelPath = "Assets/Models/Vault/Hall_Vault.fbx";
        public const string GhostModelPath = "Assets/Models/Ghost/Aldric_Ghost.fbx";
        public const string VaultPrefabPath = "Assets/PREFABS/Vault/Hall_Vault.prefab";
        public const string GhostPrefabPath = "Assets/PREFABS/Characters/Visual_NPC_Aldric_Ghost.prefab";
        public const string VaultMaterialFolder = "Assets/Materials/Vault";
        public const string GhostMaterialFolder = "Assets/Materials/Ghost";
        public const string StructureName = "Vault_Structure";
        public const string BlockerName = "Vault_Door_Blocker";
        public const string GhostName = "Ghost_Of_Sir_Aldric";

        /// <summary>The hall's inner south-east corner, the vault model's origin.</summary>
        public static readonly Vector3 VaultCorner = new Vector3(39f, 0f, -39f);

        /// <summary>Where Sir Aldric floats in front of the vault door, and his heading (toward the hall).</summary>
        public static Vector3 GhostFloorPosition => VaultCorner + new Vector3(-7.1f, 0f, 11.6f);
        public const float GhostYaw = -35f;

        /// <summary>The Blender knight is 2.7 m; scaled up he towers a little over the heroes.</summary>
        public const float GhostScale = 1.3f;

        [MenuItem("CastleOfDice/Castle Hall Vault + Aldric Ghost", false, 18)]
        public static void SetupMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupAll();
        }

        public static void SetupAll()
        {
            BuildPrefabs();
            Scene scene = EditorSceneManager.OpenScene(HallDoorsSetup.ScenePath, OpenSceneMode.Single);
            Transform review = null;
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name == "Review_Content") review = r.transform;
            }
            Transform vaultRoot = review != null ? review.Find("Queens_Vault") : null;
            Transform ghost = review != null ? review.Find(GhostName) : null;
            if (vaultRoot == null || ghost == null)
            {
                Debug.LogError("[HallVaultSetup] Queens_Vault or the ghost is missing from Review_Content.");
                return;
            }

            BuildVault(vaultRoot);

            // The ghost keeps its talk script and glow; only his body, place and heading change
            ghost.position = GhostFloorPosition + new Vector3(0f, 1.4f, 0f);
            ghost.rotation = Quaternion.Euler(0f, GhostYaw, 0f);
            Transform oldVisual;
            while ((oldVisual = ghost.Find("Visuals")) != null) Object.DestroyImmediate(oldVisual.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GhostPrefabPath), ghost);
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            visual.name = "Visuals";
            visual.transform.localPosition = new Vector3(0f, -1.4f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            DecorateGhost(ghost.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[HallVaultSetup] Vault in the south-east corner, Sir Aldric is a ghost knight.");
        }

        #region Scene

        /// <summary>
        /// Fills Queens_Vault: the strongroom, an invisible blocker in the doorway, the reward chest inside and
        /// the HallVault wiring. Keeps an existing reward chest (with its reward settings) and HallVault.
        /// </summary>
        public static void BuildVault(Transform vaultRoot)
        {
            BuildPrefabs();
            HallVault vault = vaultRoot.GetComponent<HallVault>();
            if (vault == null) vault = vaultRoot.gameObject.AddComponent<HallVault>();
            GameObject chest = vault.RewardChest;
            foreach (string old in new[] { "Vault_Door", StructureName, BlockerName })
            {
                Transform t;
                while ((t = vaultRoot.Find(old)) != null) Object.DestroyImmediate(t.gameObject);
            }

            var structure = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VaultPrefabPath), vaultRoot);
            structure.name = StructureName;
            structure.transform.position = VaultCorner;
            structure.transform.rotation = Quaternion.identity;

            Transform leaf = Find(structure.transform, "Vault_Door");
            Transform chestSpot = Find(structure.transform, "Chest_Spot");

            // Blocks the doorway while the door is shut (the leaf itself has no collider)
            var blocker = new GameObject(BlockerName);
            blocker.layer = 7; // Obstacle
            blocker.transform.SetParent(vaultRoot, false);
            Bounds lb = leaf.GetComponent<Renderer>().bounds;
            blocker.transform.position = lb.center;
            var box = blocker.AddComponent<BoxCollider>();
            box.size = new Vector3(lb.size.x, lb.size.y, Mathf.Max(0.5f, lb.size.z));

            if (chest != null)
            {
                chest.transform.position = chestSpot.position;
                chest.transform.rotation = Quaternion.identity; // lid hinge to the back wall, front toward the door
            }

            vault.Door = blocker;
            vault.DoorLeaf = leaf;
            vault.OpenAngle = InwardAngle(leaf, 100f);
            EditorUtility.SetDirty(vault);

            // The old corner pillar stood where the vault is now
            foreach (GameObject r in vaultRoot.gameObject.scene.GetRootGameObjects())
            {
                if (r.name != "Walls") continue;
                Transform pillar = r.transform.Find("Hall_Pillar_3");
                if (pillar != null) Object.DestroyImmediate(pillar.gameObject);
            }
        }

        /// <summary>The leaf swings into the vault (south): pick the sign that moves its far edge that way.</summary>
        private static float InwardAngle(Transform leaf, float degrees)
        {
            Vector3 center = leaf.GetComponent<Renderer>().bounds.center - leaf.position;
            Vector3 a = Quaternion.Euler(0f, degrees, 0f) * center;
            Vector3 b = Quaternion.Euler(0f, -degrees, 0f) * center;
            return a.z < b.z ? degrees : -degrees;
        }

        /// <summary>Gives a ghost NPC's visual its drift (the review builder strips game scripts from visuals).</summary>
        public static void DecorateGhost(GameObject npc)
        {
            Transform visual = npc.transform.Find("Visuals");
            if (visual == null) return;
            GhostFloat drift = visual.GetComponent<GhostFloat>();
            if (drift == null) drift = visual.gameObject.AddComponent<GhostFloat>();
            drift.Glow = npc.GetComponentInChildren<Light>(true);
            Light glow = drift.Glow;
            if (glow != null)
            {
                glow.transform.localPosition = new Vector3(0f, 0.7f, 0.2f);
                glow.color = new Color(0.45f, 0.85f, 1f);
                glow.range = 7f;
                glow.intensity = 2.6f;
            }
            EditorUtility.SetDirty(npc);
        }

        #endregion

        #region Prefabs

        public static void BuildPrefabs()
        {
            Material stone = HallWallsSetup.BuildMaterialIfMissing();
            EnsureFolder(VaultMaterialFolder);
            EnsureFolder(GhostMaterialFolder);
            EnsureFolder(Path.GetDirectoryName(VaultPrefabPath).Replace('\\', '/'));

            var vaultMats = new Dictionary<string, Material>
            {
                { "Vault_Stone", stone },
                { "Door_Stone", AssetDatabase.LoadAssetAtPath<Material>(CastleDoorSetup.MaterialFolder + "/M_Door_Stone.mat") },
                { "Door_StoneTrim", AssetDatabase.LoadAssetAtPath<Material>(CastleDoorSetup.MaterialFolder + "/M_Door_StoneTrim.mat") },
                { "Vault_IronPlate", Lit(VaultMaterialFolder, "M_Vault_IronPlate", new Color(0.2f, 0.21f, 0.24f), 0.55f, 0.85f) },
                { "Vault_IronDark", Lit(VaultMaterialFolder, "M_Vault_IronDark", new Color(0.08f, 0.08f, 0.09f), 0.45f, 0.8f) },
                { "Vault_Gold", Lit(VaultMaterialFolder, "M_Vault_Gold", new Color(1f, 0.74f, 0.22f), 0.7f, 1f, new Color(0.22f, 0.13f, 0.02f)) },
                { "Vault_Ruby", Lit(VaultMaterialFolder, "M_Vault_Ruby", new Color(0.8f, 0.05f, 0.12f), 0.85f, 0.2f, new Color(0.25f, 0f, 0.03f)) },
                { "Vault_Sapphire", Lit(VaultMaterialFolder, "M_Vault_Sapphire", new Color(0.1f, 0.3f, 0.95f), 0.85f, 0.2f, new Color(0f, 0.05f, 0.3f)) },
                { "Vault_ShieldBlue", Lit(VaultMaterialFolder, "M_Vault_ShieldBlue", new Color(0.14f, 0.24f, 0.55f), 0.3f, 0f) },
            };
            Import(VaultModelPath, vaultMats);

            var ghostMats = new Dictionary<string, Material>
            {
                { "Ghost_Spirit", Ghostly("M_Ghost_Spirit", new Color(0.4f, 0.8f, 1f, 0.38f), new Color(0.2f, 0.55f, 0.85f)) },
                { "Ghost_Armor", Ghostly("M_Ghost_Armor", new Color(0.55f, 0.88f, 1f, 0.5f), new Color(0.28f, 0.62f, 0.85f)) },
                { "Ghost_Steel", Ghostly("M_Ghost_Steel", new Color(0.8f, 0.97f, 1f, 0.7f), new Color(0.45f, 0.8f, 0.95f)) },
                { "Ghost_Chain", Ghostly("M_Ghost_Chain", new Color(0.3f, 0.5f, 0.62f, 0.8f), new Color(0.05f, 0.15f, 0.22f)) },
                { "Ghost_Eyes", Glowing("M_Ghost_Eyes", new Color(0.85f, 1f, 1f)) },
            };
            Import(GhostModelPath, ghostMats);

            // Vault: walls, roof and frame block; the leaf turns on its hinge; a warm light over the gold
            GameObject vaultRoot = WrapModel(VaultModelPath, "Hall_Vault", "North", Vector3.forward);
            foreach (MeshFilter mf in vaultRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name == "Vault_Walls" || mf.name == "Vault_Roof" || mf.name == "Vault_Frame")
                {
                    mf.gameObject.layer = 7; // Obstacle
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
            }
            Transform lightSpot = Find(vaultRoot.transform, "Light_Spot");
            var lightGo = new GameObject("Vault_Light");
            lightGo.transform.SetParent(vaultRoot.transform, false);
            lightGo.transform.position = lightSpot != null ? lightSpot.position - new Vector3(0f, 1.2f, 0f) : new Vector3(-4.5f, 3.6f, 4.2f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.42f);
            light.range = 9f;
            light.intensity = 2.4f;
            light.shadows = LightShadows.None;
            PrefabUtility.SaveAsPrefabAsset(vaultRoot, VaultPrefabPath);
            Object.DestroyImmediate(vaultRoot);

            // Ghost: no shadows (he is translucent), no colliders
            GameObject ghostRoot = WrapModel(GhostModelPath, "Visual_NPC_Aldric_Ghost", "Front", Vector3.forward);
            ghostRoot.transform.GetChild(0).localScale = Vector3.one * GhostScale; // a head taller than the heroes
            foreach (Renderer r in ghostRoot.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            PrefabUtility.SaveAsPrefabAsset(ghostRoot, GhostPrefabPath);
            Object.DestroyImmediate(ghostRoot);
        }

        /// <summary>Wraps a model in a root and turns it so the named marker lies along <paramref name="dir"/>.</summary>
        private static GameObject WrapModel(string modelPath, string name, string marker, Vector3 dir)
        {
            var root = new GameObject(name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);
            Transform m = Find(inst.transform, marker);
            if (m != null)
            {
                Vector3 to = m.position - root.transform.position;
                to.y = 0f;
                float yaw = Mathf.Round(Vector3.SignedAngle(to, dir, Vector3.up) / 90f) * 90f;
                inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
            return root;
        }

        private static void Import(string path, Dictionary<string, Material> mats)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[HallVaultSetup] No model at {path}.");
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
        }

        private static Material Lit(string folder, string name, Color color, float smoothness, float metallic, Color? emission = null)
        {
            Material m = LoadOrCreate(folder + "/" + name + ".mat", "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>See-through, softly glowing URP Lit; writes depth so the ghost's own parts sort cleanly.</summary>
        private static Material Ghostly(string name, Color color, Color emission)
        {
            Material m = LoadOrCreate(GhostMaterialFolder + "/" + name + ".mat", "Universal Render Pipeline/Lit");
            m.SetFloat("_Surface", 1f);       // Transparent
            m.SetFloat("_Blend", 0f);         // Alpha
            m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_Cull", (float)CullMode.Back);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", 0.75f);
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Unlit, so the eyes stay bright in the dark corner.</summary>
        private static Material Glowing(string name, Color color)
        {
            Material m = LoadOrCreate(GhostMaterialFolder + "/" + name + ".mat", "Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", color);
            m.color = color;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LoadOrCreate(string path, string shader)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        #endregion

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
