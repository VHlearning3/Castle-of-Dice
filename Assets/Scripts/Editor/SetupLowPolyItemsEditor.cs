using System.Collections.Generic;
using System.IO;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Brings in the low-poly items modelled in BlenderSources/LowPolyItems.blend (one palette texture):
    /// Othelia's signet ring, the swamp herbs, scrap piles, the Giant's Elixir, Elira's staff and Corvo's daggers.
    /// Swaps the placeholder visuals in the Forest Path and the Tower for the models, places two scrap piles in
    /// the field, and bakes where the staff and daggers sit on the hand bones (HeroClassModels hangs them there).
    /// Scenes are changed in place: only the named objects are touched.
    /// </summary>
    public static class SetupLowPolyItemsEditor
    {
        private const string ModelFolder = "Assets/Models/Items";
        private const string PalettePath = ModelFolder + "/LowPolyItems_Palette.png";
        private const string MaterialPath = ModelFolder + "/M_LowPolyItems.mat";
        private const string GlowMaterialPath = ModelFolder + "/M_LowPolyItems_Glow.mat";

        public const string RingFbx = ModelFolder + "/SignetRing.fbx";
        public const string HerbsFbx = ModelFolder + "/SwampHerbs.fbx";
        public const string ScrapFbx = ModelFolder + "/ScrapPile.fbx";
        public const string ElixirFbx = ModelFolder + "/GiantElixir.fbx";
        private const string StaffFbx = ModelFolder + "/Mage_Staff.fbx";
        private const string DaggersFbx = ModelFolder + "/Rogue_Daggers.fbx";
        private static readonly string[] AllFbx = { RingFbx, HerbsFbx, ScrapFbx, ElixirFbx, StaffFbx, DaggersFbx };

        // The weapons were fitted in Blender to these models' rest pose
        private const string MageFbx = "Assets/Characters/Player_mage_new/Mage_Unity.fbx";
        private const string RogueFbx = "Assets/Characters/Rogue+3d+model/tripo_convert_e816ce67-bf17-4244-a3c4-00e5e2b78833.fbx";
        private const string RightHand = "mixamorig:RightHand";
        private const string LeftHand = "mixamorig:LeftHand";
        private const string PlayerHeroPrefab = "Assets/PREFABS/Players/PlayerHero.prefab";

        private const string ItemPrefabFolder = "Assets/PREFABS/Items";
        private const string ScrapPickupPrefab = "Assets/PREFABS/Pickups/Pickup_ScrapPile.prefab";
        private const string ScrapRemainsPrefab = ItemPrefabFolder + "/ScrapPile_Remains.prefab";
        private const string MountsPath = "Assets/Resources/HeroWeaponMounts.asset";
        private const string LootTablePath = "Assets/Resources/LootDropTable.asset";

        private const string Zone2 = "Assets/Scenes/Zone_2_ForestPath.unity";
        private const string Zone3 = "Assets/Scenes/Zone_3_CastleCourtyard.unity";
        private const string Zone6 = "Assets/Scenes/Zone_6_Tower.unity";

        /// <summary>Visual height of each item in the world (the hero is 3 units tall).</summary>
        private const float RingHeight = 0.55f;
        private const float HerbHeight = 1.15f;
        private const float ScrapHeight = 0.95f;
        private const float ElixirHeight = 1.0f;
        private const int ScrapPerPile = 3;

        private const string ModelChild = "Model";

        [MenuItem("CastleOfDice/Setup Low-Poly Items")]
        public static void SetupAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SetupLowPolyItemsEditor] Exit Play Mode first.");
                return;
            }

            BuildAssets();
            DressForestPath();
            DressCourtyard();
            DressTower();
            Debug.Log("[SetupLowPolyItemsEditor] Low-poly items set up.");
        }

        /// <summary>Batch entry: -executeMethod CastleOfTheD20.Editor.SetupLowPolyItemsEditor.BatchSetup</summary>
        public static void BatchSetup()
        {
            SetupAll();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("CastleOfDice/Setup Low-Poly Items (assets only)")]
        public static void BuildAssets()
        {
            ConfigurePalette();
            foreach (string fbx in AllFbx) ConfigureImporter(fbx);
            AssetDatabase.Refresh();

            EnsureFolder(ItemPrefabFolder);
            BuildScrapPrefabs();
            BuildWeaponMounts();
            AssetDatabase.SaveAssets();
        }

        #region Import

        private static void ConfigurePalette()
        {
            TextureImporter importer = AssetImporter.GetAtPath(PalettePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[SetupLowPolyItemsEditor] Palette not found: {PalettePath}");
                return;
            }
            // Flat colour cells: no filtering or mips bleeding one cell into the next
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        private static void ConfigureImporter(string fbxPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[SetupLowPolyItemsEditor] Model not found: {fbxPath}");
                return;
            }
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "LP_Palette"), PaletteMaterial(false));
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "LP_Glow"), PaletteMaterial(true));
            importer.SaveAndReimport();
        }

        private static Material PaletteMaterial(bool glow)
        {
            string path = glow ? GlowMaterialPath : MaterialPath;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            mat.mainTexture = palette;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", palette);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
            if (glow)
            {
                // The elixir and the staff crystal shine with their own colour
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_EmissionMap", palette);
                mat.SetColor("_EmissionColor", Color.white * 1.6f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        #endregion

        #region Models

        /// <summary>
        /// Puts <paramref name="fbxPath"/> under <paramref name="host"/> as its "Model" child, scaled to
        /// <paramref name="height"/> world units and standing at <paramref name="groundY"/>, and hides the host's
        /// own placeholder mesh. An older Model child is replaced.
        /// </summary>
        public static GameObject AttachModel(GameObject host, string fbxPath, float height, float groundY, float yaw, Vector3 worldOffset = default)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (host == null || fbx == null)
            {
                Debug.LogWarning($"[SetupLowPolyItemsEditor] Cannot dress '{(host != null ? host.name : "null")}' with {fbxPath}.");
                return null;
            }

            Transform old = host.transform.Find(ModelChild);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            MeshRenderer placeholder = host.GetComponent<MeshRenderer>();
            if (placeholder != null) placeholder.enabled = false;

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, host.transform);
            model.name = ModelChild;
            model.transform.localPosition = Vector3.zero;
            model.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            model.transform.localScale = Vector3.one;
            SetWorldScale(model.transform, 1f);

            Bounds b = RendererBounds(model);
            float scale = height / Mathf.Max(0.01f, b.size.y);
            SetWorldScale(model.transform, scale);

            // Stand the model on groundY, centred over the host (plus any offset)
            b = RendererBounds(model);
            Vector3 anchor = new Vector3(host.transform.position.x, groundY, host.transform.position.z) + worldOffset;
            model.transform.position += anchor - new Vector3(b.center.x, b.min.y, b.center.z);

            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = host.layer;
            return model;
        }

        /// <summary>Uniform world scale even under a non-uniformly scaled parent (the parents here only scale, never tilt).</summary>
        private static void SetWorldScale(Transform t, float scale)
        {
            Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(scale / parent.x, scale / parent.y, scale / parent.z);
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool has = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static void BuildScrapPrefabs()
        {
            // Field pile: walk over it to gather the scrap
            GameObject root = new GameObject("Pickup_ScrapPile");
            try
            {
                AttachModel(root, ScrapFbx, ScrapHeight, 0f, 0f);

                SphereCollider col = root.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.9f;
                col.center = new Vector3(0f, ScrapHeight * 0.5f, 0f);

                WorldPickup pickup = root.AddComponent<WorldPickup>();
                SerializedObject so = new SerializedObject(pickup);
                so.FindProperty("kind").enumValueIndex = (int)WorldPickup.PickupKind.Scrap;
                so.FindProperty("goldAmount").intValue = 0;
                so.FindProperty("scrapAmount").intValue = ScrapPerPile;
                so.FindProperty("visual").objectReferenceValue = null; // a heap of iron does not spin
                so.FindProperty("magnetRadius").floatValue = 0f;
                so.FindProperty("promptMessage").stringValue = "Gather Scrap Metal";
                so.FindProperty("interactionRadius").floatValue = 3f;
                so.ApplyModifiedPropertiesWithoutUndo();

                foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
                PrefabUtility.SaveAsPrefabAsset(root, ScrapPickupPrefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            // Remains of a won fight: decoration only (TurnManager already paid the scrap), no collider
            root = new GameObject("ScrapPile_Remains");
            try
            {
                AttachModel(root, ScrapFbx, ScrapHeight * 0.8f, 0f, 0f);
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
                PrefabUtility.SaveAsPrefabAsset(root, ScrapRemainsPrefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            LootDropTableSO table = AssetDatabase.LoadAssetAtPath<LootDropTableSO>(LootTablePath);
            if (table != null)
            {
                table.scrapRemainsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScrapRemainsPrefab);
                EditorUtility.SetDirty(table);
            }
        }

        #endregion

        #region Hero weapons

        private static void BuildWeaponMounts()
        {
            var mounts = new List<HeroWeaponMountsSO.Mount>();
            AddMounts(mounts, CharacterClassType.Mage, MageFbx, StaffFbx, ("MageStaff", RightHand, "Weapon_MageStaff"));
            AddMounts(mounts, CharacterClassType.Rogue, RogueFbx, DaggersFbx,
                ("RogueDagger_R", RightHand, "Weapon_RogueDagger_R"), ("RogueDagger_L", LeftHand, "Weapon_RogueDagger_L"));
            AimDaggersInIdle(mounts);

            HeroWeaponMountsSO asset = AssetDatabase.LoadAssetAtPath<HeroWeaponMountsSO>(MountsPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HeroWeaponMountsSO>();
                AssetDatabase.CreateAsset(asset, MountsPath);
            }
            asset.mounts = mounts.ToArray();
            EditorUtility.SetDirty(asset);
        }

        /// <summary>
        /// The weapons file holds each weapon placed in the hero's rest-pose space (as fitted in Blender). Each one
        /// becomes a grip-pivoted mesh prefab plus its offset from the hand bone's bind pose, read from the skin.
        /// </summary>
        private static void AddMounts(List<HeroWeaponMountsSO.Mount> mounts, CharacterClassType heroClass, string heroFbx, string weaponsFbx,
            params (string mesh, string bone, string prefab)[] weapons)
        {
            GameObject heroAsset = AssetDatabase.LoadAssetAtPath<GameObject>(heroFbx);
            GameObject weaponsAsset = AssetDatabase.LoadAssetAtPath<GameObject>(weaponsFbx);
            if (heroAsset == null || weaponsAsset == null)
            {
                Debug.LogError($"[SetupLowPolyItemsEditor] Missing {heroFbx} or {weaponsFbx}.");
                return;
            }

            // Both at their file placement: a one-mesh file keeps the weapon's placement on its root
            GameObject hero = Object.Instantiate(heroAsset);
            GameObject weaponSet = Object.Instantiate(weaponsAsset);
            try
            {
                Dictionary<string, Matrix4x4> bind = BindPose(hero);

                foreach (var w in weapons)
                {
                    MeshFilter filter = FindMesh(weaponSet, w.mesh);
                    if (filter == null || !bind.TryGetValue(w.bone, out Matrix4x4 bone))
                    {
                        Debug.LogError($"[SetupLowPolyItemsEditor] '{w.mesh}' or bone '{w.bone}' missing.");
                        continue;
                    }

                    Matrix4x4 local = bone.inverse * filter.transform.localToWorldMatrix;
                    mounts.Add(new HeroWeaponMountsSO.Mount
                    {
                        heroClass = heroClass,
                        weaponPrefab = BuildWeaponPrefab(filter, w.prefab),
                        boneName = w.bone,
                        localPosition = local.GetPosition(),
                        localRotation = local.rotation,
                        localScale = local.lossyScale,
                    });
                }
            }
            finally
            {
                Object.DestroyImmediate(hero);
                Object.DestroyImmediate(weaponSet);
            }
        }

        /// <summary>
        /// Corvo's idle is Barnaby's clip, which turns the wrists differently from his own rest pose, so the
        /// daggers fitted in Blender would point backwards. Re-aim them in that idle: blades forward, a little
        /// outward and down, edges vertical. The grip position from the Blender fit stays.
        /// </summary>
        private static void AimDaggersInIdle(List<HeroWeaponMountsSO.Mount> mounts)
        {
            GameObject hero = PrefabUtility.LoadPrefabContents(PlayerHeroPrefab);
            try
            {
                HeroClassModels models = hero.GetComponent<HeroClassModels>();
                if (models == null) return;
                models.Apply(CharacterClassType.Rogue);

                Animator animator = null;
                foreach (Animator a in hero.GetComponentsInChildren<Animator>(false))
                {
                    if (a.runtimeAnimatorController != null) animator = a;
                }
                AnimationClip idle = null;
                if (animator != null)
                {
                    foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
                    {
                        if (c.name == "Idle") idle = c;
                    }
                }
                if (idle == null)
                {
                    Debug.LogWarning("[SetupLowPolyItemsEditor] Corvo's Idle clip not found; daggers keep the Blender fit.");
                    return;
                }
                idle.SampleAnimation(animator.gameObject, idle.length * 0.5f);

                Transform root = animator.transform;
                foreach (HeroWeaponMountsSO.Mount m in mounts)
                {
                    if (m.heroClass != CharacterClassType.Rogue) continue;
                    Transform bone = HeroWeaponMountsSO.FindDeep(root, m.boneName);
                    if (bone == null) continue;

                    float side = m.boneName == RightHand ? 1f : -1f;
                    Transform facing = hero.transform;
                    Vector3 blade = (facing.forward + facing.right * (0.35f * side) - facing.up * 0.35f).normalized;
                    Vector3 edge = Vector3.ProjectOnPlane(facing.up, blade).normalized;
                    // The dagger mesh runs along +Y (blade) with its edges along X
                    Quaternion world = Quaternion.LookRotation(Vector3.Cross(edge, blade), blade);
                    m.localRotation = Quaternion.Inverse(bone.rotation) * world;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(hero);
            }
        }

        /// <summary>World bind-pose matrix of every skinned bone of <paramref name="hero"/> (placed at the origin).</summary>
        private static Dictionary<string, Matrix4x4> BindPose(GameObject hero)
        {
            var result = new Dictionary<string, Matrix4x4>();
            foreach (SkinnedMeshRenderer skin in hero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Matrix4x4[] bindposes = skin.sharedMesh != null ? skin.sharedMesh.bindposes : null;
                if (bindposes == null) continue;
                for (int i = 0; i < skin.bones.Length && i < bindposes.Length; i++)
                {
                    if (skin.bones[i] == null || result.ContainsKey(skin.bones[i].name)) continue;
                    result[skin.bones[i].name] = skin.transform.localToWorldMatrix * bindposes[i].inverse;
                }
            }
            return result;
        }

        private static MeshFilter FindMesh(GameObject root, string name)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter f in filters)
            {
                if (f.name == name || (f.sharedMesh != null && f.sharedMesh.name == name)) return f;
            }
            return filters.Length == 1 ? filters[0] : null;
        }

        private static GameObject BuildWeaponPrefab(MeshFilter source, string prefabName)
        {
            GameObject go = new GameObject(prefabName);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                go.layer = 9; // Unit, like the hero carrying it
                return PrefabUtility.SaveAsPrefabAsset(go, $"{ItemPrefabFolder}/{prefabName}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        #endregion

        #region Scenes

        /// <summary>The three swamp blossom bushes get the herb model; gathering one now hides the bush.</summary>
        private static void DressForestPath()
        {
            Scene scene = EditorSceneManager.OpenScene(Zone2, OpenSceneMode.Single);
            for (int i = 1; i <= 3; i++)
            {
                GameObject herb = FindInScene("SwampHerb_" + i);
                if (herb == null) continue;
                AttachModel(herb, HerbsFbx, HerbHeight, GroundY(herb.transform.position, herb), i * 97f);

                ChestRewardInteraction reward = herb.GetComponent<ChestRewardInteraction>();
                if (reward != null)
                {
                    reward.HideWhenLooted = true;
                    EditorUtility.SetDirty(reward);
                }
            }

            // Wreckage near the zombie on the path, in the open where the camera can see it
            GameObject encounter = FindInScene("Forest_Zombie_Encounter", false);
            EnemyUnitAnchor(encounter, out Vector3 anchor);
            PlaceScrapPile(scene, "ScrapPile_Forest", FindClearSpot(anchor, 5f, 14f), 35f);
            SaveScene(scene);
        }

        private static void DressCourtyard()
        {
            Scene scene = EditorSceneManager.OpenScene(Zone3, OpenSceneMode.Single);
            Transform spawn = FindSpawn();
            Vector3 anchor = spawn != null ? spawn.position : Vector3.zero;
            PlaceScrapPile(scene, "ScrapPile_Courtyard", FindClearSpot(anchor, 4f, 12f), -20f);
            SaveScene(scene);
        }

        /// <summary>The Giant's Elixir and Othelia's ring share the treasure pedestal: the elixir behind, the ring in front.</summary>
        private static void DressTower()
        {
            Scene scene = EditorSceneManager.OpenScene(Zone6, OpenSceneMode.Single);

            GameObject pedestal = FindInScene("GiantElixir_Pedestal");
            float top = pedestal != null ? PedestalTop(pedestal) : 1f;

            GameObject vial = FindInScene("Elixir_Vial");
            if (vial != null)
            {
                Vector3 back = pedestal != null ? new Vector3(-0.15f, 0f, -0.3f) : Vector3.zero;
                AttachModel(vial, ElixirFbx, ElixirHeight, top, 200f, back);
                Collider c = vial.GetComponent<Collider>();
                if (c != null) c.enabled = false;
            }

            GameObject ring = FindInScene("SignetRing_Pickup");
            if (ring != null) DressSignetRing(ring, top);
            SaveScene(scene);
        }

        /// <summary>Swaps the signet ring pickup's built mesh for the ring model (also used by ZoneDressingBuilder).</summary>
        public static void DressSignetRing(GameObject pickup, float groundY = float.NaN)
        {
            if (float.IsNaN(groundY)) groundY = RendererBounds(pickup).min.y;
            AttachModel(pickup, RingFbx, RingHeight, groundY, 120f);
        }

        private static float PedestalTop(GameObject pedestal)
        {
            MeshRenderer r = pedestal.GetComponent<MeshRenderer>();
            return r != null ? r.bounds.max.y : pedestal.transform.position.y + pedestal.transform.lossyScale.y * 0.5f;
        }

        private static void PlaceScrapPile(Scene scene, string name, Vector3 at, float yaw)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScrapPickupPrefab);
            if (prefab == null) return;

            GameObject old = FindInScene(name, false);
            if (old != null) Object.DestroyImmediate(old);

            GameObject pile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            pile.name = name;
            at.y = GroundY(at, null);
            pile.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        }

        private static void EnemyUnitAnchor(GameObject encounter, out Vector3 anchor)
        {
            EnemyUnit enemy = encounter != null ? encounter.GetComponentInChildren<EnemyUnit>(true) : null;
            GameObject fallback = enemy == null ? FindInScene("SwampHerb_2") : null;
            anchor = enemy != null ? enemy.transform.position : fallback != null ? fallback.transform.position : Vector3.zero;
        }

        /// <summary>
        /// First spot on a ring around <paramref name="anchor"/> with nothing standing over it (trees, walls,
        /// canopies) and ground at about the anchor's height, nearest ring first.
        /// </summary>
        private static Vector3 FindClearSpot(Vector3 anchor, float minRadius, float maxRadius)
        {
            var blockers = new List<Bounds>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Bounds b = r.bounds;
                // Flat ground and whole-room shells would block everything
                if (!r.enabled || b.size.y < 0.4f || b.size.x > 30f || b.size.z > 30f) continue;
                if (r.GetComponentInParent<WorldPickup>() != null) continue;
                blockers.Add(b);
            }

            float anchorGround = GroundY(anchor, null);
            for (float radius = minRadius; radius <= maxRadius; radius += 1f)
            {
                for (int step = 0; step < 12; step++)
                {
                    float angle = step * 30f * Mathf.Deg2Rad;
                    Vector3 c = anchor + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                    var column = new Bounds(new Vector3(c.x, anchorGround + 6f, c.z), new Vector3(2.4f, 11.6f, 2.4f));
                    bool blocked = false;
                    foreach (Bounds b in blockers)
                    {
                        if (b.Intersects(column)) { blocked = true; break; }
                    }
                    if (blocked || Mathf.Abs(GroundY(c, null) - anchorGround) > 0.6f) continue;
                    Debug.Log($"[SetupLowPolyItemsEditor] Scrap pile at {c} ({radius} from {anchor}).");
                    return c;
                }
            }
            Debug.LogWarning($"[SetupLowPolyItemsEditor] No clear spot near {anchor}; using an offset.");
            return anchor + new Vector3(minRadius, 0f, 0f);
        }

        /// <summary>Highest collider surface under <paramref name="at"/>, ignoring <paramref name="self"/>.</summary>
        private static float GroundY(Vector3 at, GameObject self)
        {
            Physics.SyncTransforms();
            RaycastHit[] hits = Physics.RaycastAll(at + Vector3.up * 6f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (RaycastHit h in hits)
            {
                if (self != null && h.collider.transform.IsChildOf(self.transform)) continue;
                if (h.collider.GetComponentInParent<Interactable>() != null) continue;
                if (h.point.y > best) best = h.point.y;
            }
            return best > float.MinValue ? best : 0f;
        }

        private static Transform FindSpawn()
        {
            StartSpawnPoint spawn = Object.FindAnyObjectByType<StartSpawnPoint>(FindObjectsInactive.Include);
            return spawn != null ? spawn.transform : null;
        }

        private static GameObject FindInScene(string name, bool warn = true)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform t = HeroWeaponMountsSO.FindDeep(root.transform, name);
                if (t != null) return t.gameObject;
            }
            if (warn) Debug.LogWarning($"[SetupLowPolyItemsEditor] Not found in {SceneManager.GetActiveScene().name}: {name}");
            return null;
        }

        private static void SaveScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        #endregion
    }
}
