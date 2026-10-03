using CastleOfTheD20.Data;
using CastleOfTheD20.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Builds the gold coin and healing potion pickups that drop from fights and chests (LootDrops reads them
    /// from Resources/LootDropTable) and the village cat that meows when the hero interacts with it.
    /// </summary>
    public static class SetupWorldPropsEditor
    {
        private const string CoinFbx = "Assets/Characters/gold coin badge 3d model/gold+coin+badge+3d+model.fbx";
        private const string CoinTexture = "Assets/Characters/gold coin badge 3d model/gold+coin+badge+3d+model.fbm/gold+coin+badge+3d+model_basecolor.jpg";
        private const string PotionFbx = "Assets/Characters/healing potion 3d model/healing+potion+3d+model.fbx";
        private const string PotionTexture = "Assets/Characters/healing potion 3d model/healing+potion+3d+model.fbm/healing+potion+3d+model_basecolor.jpg";
        private const string CatFbx = "Assets/Characters/low-poly cat 3d model/low-poly+cat+3d+model.fbx";
        private const string CatTexture = "Assets/Characters/low-poly cat 3d model/low-poly+cat+3d+model.fbm/low-poly+cat+3d+model_basecolor.jpg";
        private const string PotionItem = "Assets/Data/Item_Potion_Health.asset";

        private const string PrefabFolder = "Assets/PREFABS/Pickups";
        private const string CoinPrefab = PrefabFolder + "/Pickup_GoldCoin.prefab";
        private const string PotionPrefab = PrefabFolder + "/Pickup_HealthPotion.prefab";
        private const string CatPrefab = "Assets/PREFABS/NPCs/Village_Cat.prefab";

        private const int CoinGold = 10;

        [MenuItem("CastleOfDice/Setup Pickups & Village Cat")]
        public static void SetupAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SetupWorldPropsEditor] Exit Play Mode first.");
                return;
            }

            BuildPrefabs();

            // Coins and potions drop from fights and chests (LootDrops); only the cat is placed by hand
            Scene zone1 = OpenScene("Assets/Scenes/Zone_1_VillageAndCellar.unity");
            Remove("Pickup_Coin_Village_1", "Pickup_Coin_Village_2", "Pickup_Potion_Cellar_1");
            Place(CatPrefab, "Village_Cat", "StartSpawn", 4f);
            SaveScene(zone1);

            Scene zone2 = OpenScene("Assets/Scenes/Zone_2_ForestPath.unity");
            if (Remove("Pickup_Coin_Forest_1", "Pickup_Coin_Forest_2", "Pickup_Potion_Forest_1")) SaveScene(zone2);

            Scene zone5 = OpenScene("Assets/Scenes/Zone_5_CastleHall.unity");
            if (Remove("Pickup_Potion_Hall_1")) SaveScene(zone5);

            Debug.Log("[SetupWorldPropsEditor] Loot pickups built and the village cat is in place.");
        }

        private static bool Remove(params string[] names)
        {
            bool removed = false;
            foreach (string n in names)
            {
                GameObject go = Find(n);
                if (go == null) continue;
                Object.DestroyImmediate(go);
                removed = true;
            }
            return removed;
        }

        [MenuItem("CastleOfDice/Setup Pickups & Village Cat (prefabs only)")]
        public static void BuildPrefabs()
        {
            EnsureFolder(PrefabFolder);

            GameObject coinModel = ImportStatic(CoinFbx, CoinTexture, "M_Pickup_GoldCoin");
            GameObject potionModel = ImportStatic(PotionFbx, PotionTexture, "M_Pickup_HealthPotion");
            GameObject catModel = ImportStatic(CatFbx, CatTexture, "M_Village_Cat");

            BuildPickup(CoinPrefab, "Pickup_GoldCoin", coinModel, 0.75f, WorldPickup.PickupKind.Gold, null, "Pick up Gold");
            BuildPickup(PotionPrefab, "Pickup_HealthPotion", potionModel, 0.7f, WorldPickup.PickupKind.Item,
                AssetDatabase.LoadAssetAtPath<ItemSO>(PotionItem), "Pick up Healing Potion");
            BuildCat(catModel);
            BuildLootTable();
            AssetDatabase.SaveAssets();
        }

        private const string LootTablePath = "Assets/Resources/LootDropTable.asset";

        /// <summary>Loot table read at runtime by LootDrops; keeps any numbers already tuned in it.</summary>
        private static void BuildLootTable()
        {
            LootDropTableSO table = AssetDatabase.LoadAssetAtPath<LootDropTableSO>(LootTablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LootDropTableSO>();
                AssetDatabase.CreateAsset(table, LootTablePath);
            }
            table.coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefab);
            table.potionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PotionPrefab);
            table.potionItem = AssetDatabase.LoadAssetAtPath<ItemSO>(PotionItem);
            EditorUtility.SetDirty(table);
        }


        #region Prefabs

        private static GameObject ImportStatic(string fbxPath, string texturePath, string materialName)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer != null)
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }

            string matPath = $"Assets/Characters/Materials/{materialName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            }
            EditorUtility.SetDirty(mat);

            return AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        }


        /// <summary>Model child scaled so its tallest side is <paramref name="height"/>, standing on the root.</summary>
        private static Transform AddModel(GameObject root, GameObject fbx, Material mat, float height, string name)
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.name = name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            Bounds b = new Bounds();
            bool has = false;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            float size = has ? b.size.y : 1f;
            float scale = height / Mathf.Max(0.01f, size);
            model.transform.localScale = Vector3.one * scale;
            // Stand the mesh on the root regardless of where the file put its pivot
            model.transform.localPosition = new Vector3(0f, -(b.min.y - root.transform.position.y) * scale, 0f);
            return model.transform;
        }

        private static void BuildPickup(string path, string rootName, GameObject fbx, float height, WorldPickup.PickupKind kind, ItemSO item, string prompt)
        {
            Material mat = MaterialFor(fbx);
            GameObject root = new GameObject(rootName);
            try
            {
                // The visual floats a little above the ground so it can bob and spin
                GameObject spinner = new GameObject("Visual");
                spinner.transform.SetParent(root.transform, false);
                spinner.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                AddModel(spinner, fbx, mat, height, "Model");

                SphereCollider col = root.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.7f;
                col.center = new Vector3(0f, 0.35f + height * 0.5f, 0f);

                WorldPickup pickup = root.AddComponent<WorldPickup>();
                SerializedObject so = new SerializedObject(pickup);
                so.FindProperty("kind").enumValueIndex = (int)kind;
                so.FindProperty("goldAmount").intValue = kind == WorldPickup.PickupKind.Gold ? CoinGold : 0;
                so.FindProperty("item").objectReferenceValue = item;
                so.FindProperty("visual").objectReferenceValue = spinner.transform;
                so.FindProperty("promptMessage").stringValue = prompt;
                so.FindProperty("interactionRadius").floatValue = 3f;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Ignore Raycast: dropped loot on the combat grid must not swallow tile clicks
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildCat(GameObject fbx)
        {
            Material mat = MaterialFor(fbx);
            GameObject root = new GameObject("Village_Cat");
            try
            {
                Transform model = AddModel(root, fbx, mat, 0.9f, "Model");

                BoxCollider col = root.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.45f, 0f);
                col.size = new Vector3(0.8f, 0.9f, 0.8f);

                VillageCat cat = root.AddComponent<VillageCat>();
                SerializedObject so = new SerializedObject(cat);
                so.FindProperty("visual").objectReferenceValue = model;
                so.FindProperty("promptMessage").stringValue = "Pet the Cat";
                so.FindProperty("interactionRadius").floatValue = 3f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CatPrefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material MaterialFor(GameObject fbx)
        {
            string name = fbx == null ? "" : AssetDatabase.GetAssetPath(fbx);
            if (name == CoinFbx) return AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/Materials/M_Pickup_GoldCoin.mat");
            if (name == PotionFbx) return AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/Materials/M_Pickup_HealthPotion.mat");
            return AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/Materials/M_Village_Cat.mat");
        }

        #endregion

        #region Scene placement

        /// <summary>
        /// Puts the prefab on open floor near <paramref name="anchorName"/>: tries a ring of spots at
        /// <paramref name="distance"/> and keeps the first one with ground below and nothing in the way.
        /// An object already in the scene was placed on purpose (maybe by hand), so it is refreshed on its own spot.
        /// </summary>
        private static void Place(string prefabPath, string objectName, string anchorName, float distance)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject existing = Find(objectName);
            if (existing != null && prefab != null)
            {
                // Already an instance of this prefab: prefab changes reach it on their own, so leave it alone
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing) == prefab) return;

                Vector3 keptPos = existing.transform.position;
                Quaternion keptRot = existing.transform.rotation;
                Transform keptParent = existing.transform.parent;
                Scene keptScene = existing.scene;
                Object.DestroyImmediate(existing);
                GameObject refreshed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, keptScene);
                refreshed.name = objectName;
                if (keptParent != null) refreshed.transform.SetParent(keptParent, true);
                refreshed.transform.SetPositionAndRotation(keptPos, keptRot);
                return;
            }
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject anchor = Find(anchorName);
            if (anchor == null || prefab == null)
            {
                Debug.LogWarning($"[SetupWorldPropsEditor] Could not place {objectName}: anchor '{anchorName}' or prefab missing.");
                return;
            }

            Vector3 origin = anchor.transform.position;
            for (int ring = 0; ring < 3; ring++)
            {
                float d = distance + ring * 1.5f;
                for (int i = 0; i < 12; i++)
                {
                    float angle = (i * 30f + ring * 15f) * Mathf.Deg2Rad;
                    Vector3 probe = origin + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * d;
                    if (!TryGround(probe, origin.y, out Vector3 ground)) continue;
                    if (Physics.CheckSphere(ground + Vector3.up * 0.7f, 0.55f, ~0, QueryTriggerInteraction.Ignore)) continue;

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor.scene);
                    instance.name = objectName;
                    instance.transform.position = ground;
                    instance.transform.rotation = Quaternion.LookRotation(new Vector3(origin.x - ground.x, 0f, origin.z - ground.z).normalized, Vector3.up);
                    return;
                }
            }
            Debug.LogWarning($"[SetupWorldPropsEditor] No free floor near '{anchorName}' for {objectName}.");
        }

        private static bool TryGround(Vector3 probe, float anchorY, out Vector3 ground)
        {
            ground = Vector3.zero;
            if (!Physics.Raycast(new Vector3(probe.x, anchorY + 3f, probe.z), Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.point.y - anchorY) > 1.5f || hit.normal.y < 0.9f) return false;
            ground = hit.point;
            return true;
        }

        private static GameObject Find(string name)
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name == name && go.scene.IsValid()) return go;
            }
            return null;
        }

        private static Scene OpenScene(string path)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path == path) return active;
            if (active.isDirty && !string.IsNullOrEmpty(active.path)) EditorSceneManager.SaveScene(active);
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static void SaveScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        #endregion
    }
}
