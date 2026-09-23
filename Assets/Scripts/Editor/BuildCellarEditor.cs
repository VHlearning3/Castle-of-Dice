using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;
using CastleOfTheD20.Combat;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Editor utility that constructs the underground Cellar room from cubes under village_ground,
    /// sets up lighting, exit barriers, 3 giant rat enemies, a reward chest, and builds the
    /// interactive teleporting cellar hatch next to NPC_Barnaby.
    /// </summary>
    public static class BuildCellarEditor
    {
        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            EditorApplication.delayCall += CheckAndBuild;
            EditorSceneManager.sceneOpened += (scene, mode) =>
            {
                EditorApplication.delayCall += CheckAndBuild;
            };
        }

        private static void CheckAndBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.name == "StartVillage")
            {
                EnsureStartSpawnConfigured();

                GameObject cellar = GameObject.Find("Cellar_Chamber");
                if (cellar == null)
                {
                    Debug.Log("[BuildCellarEditor] StartVillage active and Cellar_Chamber not found. Auto-generating Cellar and Door...");
                    BuildCellarAndDoor(false);
                    EnsureStartSpawnConfigured();
                }
                else
                {
                    EnsureChestConfigured();
                    EnsureSpawnAndBarrierConfigured();
                    EnsureCombatUIConfigured();
                    EnsureCellarEnemiesConfigured();
                    EnsureVillageHatchConfigured();

                    // Check if children are misaligned (e.g. user dragged geometry separately)
                    Transform geo = cellar.transform.Find("Cellar_Geometry");
                    Transform trigger = cellar.transform.Find("Cellar_Encounter_Trigger");
                    Transform grid = cellar.transform.Find("CombatGrid");
                    if ((geo != null && geo.localPosition.sqrMagnitude > 0.01f) ||
                        (trigger != null && Mathf.Abs(trigger.localPosition.y - 2.0f) > 0.1f) ||
                        grid == null)
                    {
                        Debug.Log("[BuildCellarEditor] Detected misaligned or missing Cellar_Chamber pieces. Auto-aligning...");
                        RealignCellarPiecesMenu();
                    }
                }
            }
        }

        [MenuItem("CastleOfDice/Create or Select StartSpawn Cube")]
        public static void CreateOrSelectStartSpawnMenu()
        {
            GameObject spawnObj = EnsureStartSpawnConfigured(true);
            if (spawnObj != null)
            {
                Selection.activeGameObject = spawnObj;
                EditorGUIUtility.PingObject(spawnObj);
            }
        }

        public static GameObject EnsureStartSpawnConfigured(bool forceSelect = false)
        {
            GameObject spawnObj = GameObject.Find("StartSpawn");
            if (spawnObj == null)
            {
                spawnObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spawnObj.name = "StartSpawn";
                Undo.RegisterCreatedObjectUndo(spawnObj, "Create StartSpawn Cube");

                PlayerExplorationMovement player = Object.FindAnyObjectByType<PlayerExplorationMovement>();
                if (player != null)
                {
                    spawnObj.transform.position = new Vector3(player.transform.position.x, 0.2f, player.transform.position.z);
                    spawnObj.transform.rotation = player.transform.rotation;
                }
                else
                {
                    spawnObj.transform.position = new Vector3(7.9f, 0.2f, -9.2f);
                    spawnObj.transform.rotation = Quaternion.identity;
                }

                spawnObj.transform.localScale = new Vector3(1.2f, 0.4f, 1.2f);

                BoxCollider col = spawnObj.GetComponent<BoxCollider>();
                if (col != null) col.isTrigger = true;

                Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Grass_1.mat")
                    ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Crystals.mat");
                if (mat != null)
                {
                    MeshRenderer mr = spawnObj.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sharedMaterial = mat;
                }

                spawnObj.AddComponent<StartSpawnPoint>();

                EditorSceneManager.MarkSceneDirty(spawnObj.scene);
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorSceneManager.SaveScene(spawnObj.scene);
                }
                Debug.Log("[BuildCellarEditor] Created StartSpawn cube in StartVillage scene.");
            }
            else
            {
                StartSpawnPoint spawnComp = spawnObj.GetComponent<StartSpawnPoint>();
                if (spawnComp == null)
                {
                    spawnComp = Undo.AddComponent<StartSpawnPoint>(spawnObj);
                    EditorSceneManager.MarkSceneDirty(spawnObj.scene);
                }
                BoxCollider col = spawnObj.GetComponent<BoxCollider>();
                if (col != null && !col.isTrigger)
                {
                    col.isTrigger = true;
                    EditorSceneManager.MarkSceneDirty(spawnObj.scene);
                }
            }

            if (forceSelect && spawnObj != null)
            {
                Selection.activeGameObject = spawnObj;
                EditorGUIUtility.PingObject(spawnObj);
            }

            return spawnObj;
        }

        private static void EnsureCombatUIConfigured()
        {
            CombatUIController ui = Object.FindAnyObjectByType<CombatUIController>(FindObjectsInactive.Include);
            if (ui != null && !ui.gameObject.activeSelf)
            {
                Undo.RecordObject(ui.gameObject, "Activate CombatActionBar");
                ui.gameObject.SetActive(true);
                EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
                EditorSceneManager.SaveScene(ui.gameObject.scene);
                Debug.Log("[BuildCellarEditor] Auto-activated CombatActionBar GameObject in StartVillage scene.");
            }
        }

        private static void EnsureChestConfigured()
        {
            GameObject cellar = GameObject.Find("Cellar_Chamber");
            if (cellar != null)
            {
                Transform chest = cellar.transform.Find("Cellar_Reward_Chest");
                if (chest != null)
                {
                    bool modified = false;
                    BoxCollider col = chest.GetComponent<BoxCollider>();
                    if (col == null)
                    {
                        col = chest.gameObject.AddComponent<BoxCollider>();
                        modified = true;
                    }
                    if (col.center != new Vector3(0f, 0.45f, 0f) || col.size != new Vector3(1.4f, 0.9f, 1.0f))
                    {
                        col.center = new Vector3(0f, 0.45f, 0f);
                        col.size = new Vector3(1.4f, 0.9f, 1.0f);
                        modified = true;
                    }

                    ChestRewardInteraction chestReward = chest.GetComponent<ChestRewardInteraction>();
                    if (chestReward == null)
                    {
                        chestReward = chest.gameObject.AddComponent<ChestRewardInteraction>();
                        modified = true;
                    }
                    if (chestReward.GoldReward != 30)
                    {
                        chestReward.GoldReward = 30;
                        modified = true;
                    }

                    if (modified)
                    {
                        EditorSceneManager.MarkSceneDirty(chest.gameObject.scene);
                        EditorSceneManager.SaveScene(chest.gameObject.scene);
                        Debug.Log("[BuildCellarEditor] Auto-configured Cellar_Reward_Chest with BoxCollider and ChestRewardInteraction (30 gold) and saved scene.");
                    }
                }
            }
        }

        private static void EnsureSpawnAndBarrierConfigured()
        {
            GameObject cellar = GameObject.Find("Cellar_Chamber");
            if (cellar != null)
            {
                bool modified = false;
                Transform spawn = cellar.transform.Find("Cellar_PlayerSpawnPoint");
                if (spawn != null && (spawn.localPosition - new Vector3(0f, 0.2f, -5.0f)).sqrMagnitude > 0.001f)
                {
                    spawn.localPosition = new Vector3(0f, 0.2f, -5.0f);
                    modified = true;
                }

                Transform barrier = cellar.transform.Find("Cellar_Exit_Barrier");
                if (barrier != null && (barrier.localPosition - new Vector3(0f, 2.0f, -7.5f)).sqrMagnitude > 0.001f)
                {
                    barrier.localPosition = new Vector3(0f, 2.0f, -7.5f);
                    modified = true;
                }

                if (modified)
                {
                    EditorSceneManager.MarkSceneDirty(cellar.scene);
                    EditorSceneManager.SaveScene(cellar.scene);
                    Debug.Log("[BuildCellarEditor] Auto-aligned Cellar_PlayerSpawnPoint and Cellar_Exit_Barrier to prevent combat spawn blockage.");
                }
            }
        }

        private static void EnsureCellarEnemiesConfigured()
        {
            GameObject cellar = GameObject.Find("Cellar_Chamber");
            if (cellar != null)
            {
                Transform enemies = cellar.transform.Find("Cellar_Enemies");
                if (enemies != null)
                {
                    bool modified = false;
                    foreach (Transform child in enemies)
                    {
                        if (child.gameObject.activeSelf)
                        {
                            Undo.RecordObject(child.gameObject, "Deactivate Cellar Enemy on Start");
                            child.gameObject.SetActive(false);
                            modified = true;
                        }
                    }
                    if (modified)
                    {
                        EditorSceneManager.MarkSceneDirty(cellar.scene);
                        if (!EditorApplication.isPlayingOrWillChangePlaymode)
                        {
                            EditorSceneManager.SaveScene(cellar.scene);
                        }
                        Debug.Log("[BuildCellarEditor] Auto-deactivated Cellar_Enemies so they wait for Cellar_Encounter_Trigger.");
                    }
                }
            }
        }

        private static void EnsureVillageHatchConfigured()
        {
            GameObject hatch = GameObject.Find("Village_Cellar_Hatch");
            if (hatch != null)
            {
                DoorTeleporter dt = hatch.GetComponent<DoorTeleporter>();
                if (dt != null && dt.TriggerOnWalk)
                {
                    Undo.RecordObject(dt, "Set Hatch Click to Interact");
                    dt.TriggerOnWalk = false;
                    EditorSceneManager.MarkSceneDirty(hatch.scene);
                    if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        EditorSceneManager.SaveScene(hatch.scene);
                    }
                    Debug.Log("[BuildCellarEditor] Configured Village_Cellar_Hatch to require click interaction (triggerOnWalk = false).");
                }
            }
        }

        [MenuItem("CastleOfDice/Re-align Cellar Pieces")]
        public static void RealignCellarPiecesMenu()
        {
            GameObject cellar = GameObject.Find("Cellar_Chamber");
            if (cellar == null)
            {
                Debug.LogWarning("[BuildCellarEditor] Cellar_Chamber not found. Building fresh cellar...");
                BuildCellarAndDoor(true);
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(cellar, "Re-align Cellar Pieces");

            DungeonRoomController roomCtrl = cellar.GetComponentInChildren<DungeonRoomController>(true);
            if (roomCtrl != null)
            {
                roomCtrl.AlignCellarComponents();
            }

            // Ensure geometry is at (0, 0, 0)
            Transform geo = cellar.transform.Find("Cellar_Geometry");
            if (geo != null) geo.localPosition = Vector3.zero;

            Transform lighting = cellar.transform.Find("Cellar_Lighting");
            if (lighting != null) lighting.localPosition = Vector3.zero;

            Transform ladder = cellar.transform.Find("Cellar_Exit_Ladder");
            if (ladder != null) ladder.localPosition = new Vector3(0f, 0f, -8.4f);

            Transform spawn = cellar.transform.Find("Cellar_PlayerSpawnPoint");
            if (spawn != null) spawn.localPosition = new Vector3(0f, 0.2f, -5.0f);

            Transform barrier = cellar.transform.Find("Cellar_Exit_Barrier");
            if (barrier != null) barrier.localPosition = new Vector3(0f, 2.0f, -7.5f);

            Transform chest = cellar.transform.Find("Cellar_Reward_Chest");
            if (chest != null)
            {
                chest.localPosition = new Vector3(0f, 0f, 7.2f);
                ChestRewardInteraction chestReward = chest.GetComponent<ChestRewardInteraction>();
                if (chestReward == null)
                {
                    chestReward = Undo.AddComponent<ChestRewardInteraction>(chest.gameObject);
                }
                chestReward.GoldReward = 30;
                chestReward.EnsureChestCollider();
            }

            Transform enemies = cellar.transform.Find("Cellar_Enemies");
            if (enemies != null) enemies.localPosition = Vector3.zero;

            Transform trigger = cellar.transform.Find("Cellar_Encounter_Trigger");
            if (trigger != null) trigger.localPosition = new Vector3(0f, 2.0f, 0f);

            // Ensure CombatGrid is parented under Cellar_Chamber
            GridManager cellarGrid = cellar.GetComponentInChildren<GridManager>(true);
            if (cellarGrid == null)
            {
                GridManager[] allGrids = Object.FindObjectsByType<GridManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var g in allGrids)
                {
                    if (g.transform.parent == null)
                    {
                        Undo.SetTransformParent(g.transform, cellar.transform, "Parent Grid to Cellar");
                        g.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                        cellarGrid = g;
                        break;
                    }
                }

                if (cellarGrid == null)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/CombatGrid.prefab");
                    if (prefab != null)
                    {
                        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cellar.transform);
                        go.name = "CombatGrid";
                        go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                        cellarGrid = go.GetComponent<GridManager>();
                    }
                }
            }

            if (cellarGrid != null)
            {
                cellarGrid.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                if (cellarGrid.Tiles.Count == 0)
                {
                    cellarGrid.GenerateGrid(8, 8);
                }
            }

            // Remove any other root GridManagers
            GridManager[] sceneGridManagers = Object.FindObjectsByType<GridManager>(FindObjectsSortMode.None);
            foreach (var gm in sceneGridManagers)
            {
                if (cellarGrid != null && gm != cellarGrid && gm.transform.parent == null)
                {
                    Undo.DestroyObjectImmediate(gm.gameObject);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[BuildCellarEditor] Cellar_Chamber and all pieces have been successfully re-aligned and locked together!");
        }

        [MenuItem("CastleOfDice/Build Cellar and Door")]
        public static void BuildCellarAndDoorMenu()
        {
            BuildCellarAndDoor(true);
        }

        public static void BuildCellarAndDoor(bool interactive = false)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.name != "StartVillage")
            {
                activeScene = EditorSceneManager.OpenScene("Assets/Scenes/StartVillage.unity", OpenSceneMode.Single);
            }

            Undo.SetCurrentGroupName("Build Cellar and Door");
            int undoGroup = Undo.GetCurrentGroup();

            // 1. Locate materials
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Rocks_1_2.mat");
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Logs.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_BoxeS.mat");
            Material metalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_HammerJAanvil.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Stone_fence.mat");
            Material goldMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Chest.mat");
            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Chest.prefab");

            // 2. Clean up existing Cellar root or legacy controllers if present
            Vector3 cellarPosition = new Vector3(0f, -15f, 0f);
            GameObject existingCellar = GameObject.Find("Cellar_Chamber");
            if (existingCellar != null)
            {
                cellarPosition = existingCellar.transform.position;
                Undo.DestroyObjectImmediate(existingCellar);
            }
            GameObject existingHatch = GameObject.Find("Village_Cellar_Hatch");
            if (existingHatch != null)
            {
                Undo.DestroyObjectImmediate(existingHatch);
            }
            GameObject legacyController = GameObject.Find("DungeonRoomController");
            if (legacyController != null && legacyController.transform.parent == null)
            {
                Undo.DestroyObjectImmediate(legacyController);
            }

            // 3. Create Root Hierarchy for the Cellar
            GameObject cellarRoot = new GameObject("Cellar_Chamber");
            Undo.RegisterCreatedObjectUndo(cellarRoot, "Create Cellar Chamber");
            cellarRoot.transform.position = cellarPosition;

            // ==========================================
            // A. CELLAR ARCHITECTURE (BUILT FROM CUBES)
            // ==========================================
            GameObject geoContainer = new GameObject("Cellar_Geometry");
            geoContainer.transform.SetParent(cellarRoot.transform, false);

            // Floor (18 x 1 x 18 cube, top at y = -15.0)
            GameObject floor = CreateCube("Cellar_Floor", new Vector3(0f, -0.5f, 0f), new Vector3(18f, 1f, 18f), wallMat, geoContainer.transform);

            // Ceiling (18 x 1 x 18 cube, bottom at y = -10.0 -> local y = +5.0)
            GameObject ceiling = CreateCube("Cellar_Ceiling", new Vector3(0f, 5.5f, 0f), new Vector3(18f, 1f, 18f), wallMat, geoContainer.transform);

            // Walls (5m tall, enclosing the 18x18 room)
            GameObject wallNorth = CreateCube("Wall_North", new Vector3(0f, 2.5f, 9f), new Vector3(18f, 5f, 1f), wallMat, geoContainer.transform);
            GameObject wallSouth = CreateCube("Wall_South", new Vector3(0f, 2.5f, -9f), new Vector3(18f, 5f, 1f), wallMat, geoContainer.transform);
            GameObject wallEast  = CreateCube("Wall_East",  new Vector3(9f, 2.5f, 0f), new Vector3(1f, 5f, 18f), wallMat, geoContainer.transform);
            GameObject wallWest  = CreateCube("Wall_West",  new Vector3(-9f, 2.5f, 0f), new Vector3(1f, 5f, 18f), wallMat, geoContainer.transform);

            // 4 Stone Support Pillars
            CreateCube("Pillar_NW", new Vector3(-4.5f, 2.5f, 4.5f), new Vector3(1.2f, 5f, 1.2f), wallMat, geoContainer.transform);
            CreateCube("Pillar_NE", new Vector3(4.5f, 2.5f, 4.5f), new Vector3(1.2f, 5f, 1.2f), wallMat, geoContainer.transform);
            CreateCube("Pillar_SW", new Vector3(-4.5f, 2.5f, -4.5f), new Vector3(1.2f, 5f, 1.2f), wallMat, geoContainer.transform);
            CreateCube("Pillar_SE", new Vector3(4.5f, 2.5f, -4.5f), new Vector3(1.2f, 5f, 1.2f), wallMat, geoContainer.transform);

            // ==========================================
            // B. CELLAR ATMOSPHERIC LIGHTING
            // ==========================================
            GameObject lightContainer = new GameObject("Cellar_Lighting");
            lightContainer.transform.SetParent(cellarRoot.transform, false);

            // Central warm dungeon lamp
            GameObject centerLight = new GameObject("TorchLight_Center");
            centerLight.transform.SetParent(lightContainer.transform, false);
            centerLight.transform.localPosition = new Vector3(0f, 3.8f, 0f);
            Light pLight = centerLight.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = new Color(1.0f, 0.72f, 0.38f); // Warm flame
            pLight.range = 22f;
            pLight.intensity = 2.8f;
            pLight.shadows = LightShadows.Soft;

            // Sconce West
            GameObject westLight = new GameObject("TorchLight_West");
            westLight.transform.SetParent(lightContainer.transform, false);
            westLight.transform.localPosition = new Vector3(-8.2f, 2.8f, 0f);
            Light wLight = westLight.AddComponent<Light>();
            wLight.type = LightType.Point;
            wLight.color = new Color(1.0f, 0.65f, 0.28f);
            wLight.range = 14f;
            wLight.intensity = 1.8f;

            // Sconce East
            GameObject eastLight = new GameObject("TorchLight_East");
            eastLight.transform.SetParent(lightContainer.transform, false);
            eastLight.transform.localPosition = new Vector3(8.2f, 2.8f, 0f);
            Light eLight = eastLight.AddComponent<Light>();
            eLight.type = LightType.Point;
            eLight.color = new Color(1.0f, 0.65f, 0.28f);
            eLight.range = 14f;
            eLight.intensity = 1.8f;

            // ==========================================
            // C. CELLAR LANDING & RETURN LADDER
            // ==========================================
            // Player spawn point when entering the cellar
            GameObject cellarSpawnPoint = new GameObject("Cellar_PlayerSpawnPoint");
            cellarSpawnPoint.transform.SetParent(cellarRoot.transform, false);
            cellarSpawnPoint.transform.localPosition = new Vector3(0f, 0.2f, -5.0f);
            cellarSpawnPoint.transform.localRotation = Quaternion.Euler(0f, 0f, 0f); // Facing north into room

            // Exit ladder against the South wall
            GameObject ladderObj = new GameObject("Cellar_Exit_Ladder");
            ladderObj.transform.SetParent(cellarRoot.transform, false);
            ladderObj.transform.localPosition = new Vector3(0f, 0f, -8.4f);

            // Ladder visual rungs from cubes
            CreateCube("Ladder_Rail_L", new Vector3(-0.4f, 2.5f, 0f), new Vector3(0.08f, 5f, 0.08f), woodMat, ladderObj.transform);
            CreateCube("Ladder_Rail_R", new Vector3(0.4f, 2.5f, 0f), new Vector3(0.08f, 5f, 0.08f), woodMat, ladderObj.transform);
            for (float rungY = 0.5f; rungY <= 4.5f; rungY += 0.5f)
            {
                CreateCube($"Rung_{rungY}", new Vector3(0f, rungY, 0f), new Vector3(0.72f, 0.06f, 0.06f), woodMat, ladderObj.transform);
            }

            // Ladder interactable teleporter
            BoxCollider ladderCol = ladderObj.AddComponent<BoxCollider>();
            ladderCol.center = new Vector3(0f, 1.5f, 0f);
            ladderCol.size = new Vector3(1.5f, 3f, 1f);
            ladderCol.isTrigger = true;

            DoorTeleporter ladderTeleporter = ladderObj.AddComponent<DoorTeleporter>();
            ladderTeleporter.InitializeTeleporter(null, "Climb Ladder to Village", 3.5f);

            // ==========================================
            // D. ENCOUNTER BARRIERS & GATES
            // ==========================================
            // Iron cellar portcullis that locks down over the ladder exit during combat
            GameObject exitBarrier = CreateCube("Cellar_Exit_Barrier", new Vector3(0f, 2.0f, -7.5f), new Vector3(3.2f, 4.0f, 0.3f), metalMat, cellarRoot.transform);
            // Inactive by default
            exitBarrier.SetActive(false);

            // ==========================================
            // E. CELLAR ENEMIES (3 GIANT CELLAR RATS)
            // ==========================================
            GameObject enemiesContainer = new GameObject("Cellar_Enemies");
            enemiesContainer.transform.SetParent(cellarRoot.transform, false);

            List<GameObject> enemyList = new List<GameObject>();
            Vector3[] ratPositions = new Vector3[]
            {
                new Vector3(-3.5f, 0.35f, 2.5f),
                new Vector3(0.0f, 0.35f, 5.0f),
                new Vector3(3.5f, 0.35f, 2.5f)
            };

            for (int i = 0; i < ratPositions.Length; i++)
            {
                GameObject ratObj = CreateRatEnemy($"Cellar_Pest_{i + 1}", ratPositions[i], woodMat, metalMat, enemiesContainer.transform);
                ratObj.SetActive(false); // Inactive by default
                enemyList.Add(ratObj);
            }

            // ==========================================
            // F. REWARD CHEST
            // ==========================================
            GameObject chestObj;
            if (chestPrefab != null)
            {
                chestObj = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, cellarRoot.transform);
                chestObj.name = "Cellar_Reward_Chest";
                chestObj.transform.localPosition = new Vector3(0f, 0f, 7.2f);
                chestObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                // Fallback decorative chest built from cubes
                chestObj = CreateChest("Cellar_Reward_Chest", new Vector3(0f, 0.4f, 7.2f), woodMat, goldMat, cellarRoot.transform);
            }

            // Ensure ChestRewardInteraction and BoxCollider with 30 gold
            ChestRewardInteraction rewardComp = chestObj.GetComponent<ChestRewardInteraction>();
            if (rewardComp == null)
            {
                rewardComp = chestObj.AddComponent<ChestRewardInteraction>();
            }
            rewardComp.GoldReward = 30;
            rewardComp.EnsureChestCollider();

            chestObj.SetActive(false); // Inactive by default

            // ==========================================
            // G. DUNGEON ROOM CONTROLLER (ENCOUNTER MANAGER)
            // ==========================================
            GameObject encounterTriggerObj = new GameObject("Cellar_Encounter_Trigger");
            encounterTriggerObj.transform.SetParent(cellarRoot.transform, false);
            encounterTriggerObj.transform.localPosition = new Vector3(0f, 2.0f, 0f);

            BoxCollider roomTrigger = encounterTriggerObj.AddComponent<BoxCollider>();
            roomTrigger.center = Vector3.zero;
            roomTrigger.size = new Vector3(15f, 4.5f, 13f);
            roomTrigger.isTrigger = true;

            DungeonRoomController roomController = encounterTriggerObj.AddComponent<DungeonRoomController>();
            roomController.roomLocation = "Cellar";
            roomController.bossIdentifier = "";
            roomController.exitBarriers = new List<GameObject> { exitBarrier };
            roomController.roomEnemies = enemyList;
            roomController.secretPassageOrChest = chestObj;

            // ==========================================
            // H. THE DOOR NEXT TO NPC_BARNABY IN VILLAGE
            // ==========================================
            // Barnaby is at approx (-0.1, 1.2, -5.5). We place the hatch right next to him at (2.2, 0.05, -5.5).
            GameObject villageHatch = new GameObject("Village_Cellar_Hatch");
            Undo.RegisterCreatedObjectUndo(villageHatch, "Create Village Cellar Hatch");
            villageHatch.transform.position = new Vector3(2.2f, 0.05f, -5.5f);
            villageHatch.transform.rotation = Quaternion.Euler(0f, -15f, 0f);

            // Hatch frame & angled wooden doors built from cubes
            CreateCube("Hatch_Frame", new Vector3(0f, 0.08f, 0f), new Vector3(2.0f, 0.16f, 2.2f), woodMat, villageHatch.transform);
            CreateCube("Hatch_Door_Left", new Vector3(-0.45f, 0.18f, 0f), new Vector3(0.85f, 0.08f, 1.9f), woodMat, villageHatch.transform);
            CreateCube("Hatch_Door_Right", new Vector3(0.45f, 0.18f, 0f), new Vector3(0.85f, 0.08f, 1.9f), woodMat, villageHatch.transform);
            CreateCube("Hatch_Handle_L", new Vector3(-0.15f, 0.24f, 0f), new Vector3(0.1f, 0.06f, 0.3f), metalMat, villageHatch.transform);
            CreateCube("Hatch_Handle_R", new Vector3(0.15f, 0.24f, 0f), new Vector3(0.1f, 0.06f, 0.3f), metalMat, villageHatch.transform);

            // Interaction collider & DoorTeleporter
            BoxCollider hatchCol = villageHatch.AddComponent<BoxCollider>();
            hatchCol.center = new Vector3(0f, 0.5f, 0f);
            hatchCol.size = new Vector3(2.4f, 1.2f, 2.4f);
            hatchCol.isTrigger = true;

            DoorTeleporter hatchTeleporter = villageHatch.AddComponent<DoorTeleporter>();
            hatchTeleporter.InitializeTeleporter(cellarSpawnPoint.transform, "Enter Barnaby's Cellar", 4.0f);

            // Landing spot in the village when climbing up the cellar ladder
            GameObject villageExitPoint = new GameObject("Village_PlayerExitPoint");
            villageExitPoint.transform.position = new Vector3(2.2f, 1.2f, -3.8f);
            villageExitPoint.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Undo.RegisterCreatedObjectUndo(villageExitPoint, "Create Village Player Exit Point");

            // Wire up the cellar ladder destination to this village landing spot
            ladderTeleporter.Destination = villageExitPoint.transform;

            // ==========================================
            // I. TACTICAL COMBAT GRID PREFAB
            // ==========================================
            GameObject combatGridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/CombatGrid.prefab");
            GameObject gridObj;
            if (combatGridPrefab != null)
            {
                gridObj = (GameObject)PrefabUtility.InstantiatePrefab(combatGridPrefab, cellarRoot.transform);
                gridObj.name = "CombatGrid";
                gridObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                gridObj.transform.localRotation = Quaternion.identity;
                gridObj.transform.localScale = Vector3.one;
            }
            else
            {
                gridObj = new GameObject("CombatGrid");
                gridObj.transform.SetParent(cellarRoot.transform, false);
                gridObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                gridObj.AddComponent<GridManager>();
            }

            GridManager gridMgr = gridObj.GetComponent<GridManager>();
            if (gridMgr != null)
            {
                gridMgr.GenerateGrid(8, 8);
            }

            // Remove any old root GridManager from the scene so there are no duplicates
            GridManager[] sceneGridManagers = Object.FindObjectsByType<GridManager>(FindObjectsSortMode.None);
            foreach (var gm in sceneGridManagers)
            {
                if (gm.gameObject != gridObj && gm.transform.parent == null)
                {
                    Undo.DestroyObjectImmediate(gm.gameObject);
                }
            }

            // Ensure StartSpawn cube is present and configured
            EnsureStartSpawnConfigured();
            EnsureCellarEnemiesConfigured();
            EnsureVillageHatchConfigured();

            // Mark scene dirty and save
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[BuildCellarEditor] Successfully built Cellar_Chamber under village_ground and Village_Cellar_Hatch next to NPC_Barnaby!");
            if (interactive)
            {
                EditorUtility.DisplayDialog("Build Cellar & Door", "Successfully constructed:\n\n1. Underground Cellar Chamber (floor, ceiling, walls, pillars, lighting)\n2. Village Cellar Hatch next to NPC_Barnaby\n3. Bi-directional teleportation (Hatch <-> Cellar Ladder)\n4. Cellar Encounter (3 Giant Rats, locking gate, reward chest)", "OK");
            }
        }

        #region Helper Creation Methods

        private static GameObject CreateCube(string name, Vector3 localPos, Vector3 localScale, Material mat, Transform parent)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = localScale;

            if (mat != null)
            {
                MeshRenderer mr = cube.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }

            return cube;
        }

        private static GameObject CreateRatEnemy(string name, Vector3 localPos, Material bodyMat, Material eyeMat, Transform parent)
        {
            GameObject rat = new GameObject(name);
            rat.transform.SetParent(parent, false);
            rat.transform.localPosition = localPos;
            rat.tag = "Enemy";

            // Rat body (low poly cube)
            GameObject body = CreateCube("Body", new Vector3(0f, 0f, 0f), new Vector3(0.9f, 0.6f, 1.3f), bodyMat, rat.transform);
            // Rat head
            GameObject head = CreateCube("Head", new Vector3(0f, 0.1f, 0.75f), new Vector3(0.5f, 0.4f, 0.5f), bodyMat, rat.transform);
            // Glowing red eyes
            GameObject eyeL = CreateCube("Eye_L", new Vector3(-0.16f, 0.22f, 0.95f), new Vector3(0.08f, 0.08f, 0.08f), eyeMat, rat.transform);
            GameObject eyeR = CreateCube("Eye_R", new Vector3(0.16f, 0.22f, 0.95f), new Vector3(0.08f, 0.08f, 0.08f), eyeMat, rat.transform);

            // Combat Unit Setup
            BoxCollider col = rat.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.3f, 0.2f);
            col.size = new Vector3(1.0f, 0.8f, 1.6f);

            EnemyUnit enemy = rat.AddComponent<EnemyUnit>();
            // Use reflection or serialized property assignments for profile
            var hpField = typeof(CombatUnit).GetField("maxHP", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hpField != null) hpField.SetValue(enemy, 12);

            var currHpField = typeof(CombatUnit).GetField("currentHP", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (currHpField != null) currHpField.SetValue(enemy, 12);

            var nameField = typeof(CombatUnit).GetField("unitName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (nameField != null) nameField.SetValue(enemy, "Giant Cellar Rat");

            var dmgField = typeof(EnemyUnit).GetField("attackDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dmgField != null) dmgField.SetValue(enemy, 4);

            var bonusField = typeof(EnemyUnit).GetField("attackBonus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (bonusField != null) bonusField.SetValue(enemy, 2);

            return rat;
        }

        private static GameObject CreateChest(string name, Vector3 localPos, Material woodMat, Material goldMat, Transform parent)
        {
            GameObject chest = new GameObject(name);
            chest.transform.SetParent(parent, false);
            chest.transform.localPosition = localPos;

            // Chest base
            CreateCube("Chest_Base", new Vector3(0f, 0.25f, 0f), new Vector3(1.2f, 0.5f, 0.8f), woodMat, chest.transform);
            // Chest lid
            CreateCube("Chest_Lid", new Vector3(0f, 0.58f, 0f), new Vector3(1.24f, 0.2f, 0.84f), woodMat, chest.transform);
            // Gold latch & bands
            CreateCube("Chest_Latch", new Vector3(0f, 0.45f, -0.42f), new Vector3(0.2f, 0.2f, 0.06f), goldMat, chest.transform);
            CreateCube("Band_Left", new Vector3(-0.4f, 0.38f, 0f), new Vector3(0.08f, 0.65f, 0.84f), goldMat, chest.transform);
            CreateCube("Band_Right", new Vector3(0.4f, 0.38f, 0f), new Vector3(0.08f, 0.65f, 0.84f), goldMat, chest.transform);

            BoxCollider col = chest.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.size = new Vector3(1.4f, 0.9f, 1.0f);

            ChestRewardInteraction reward = chest.AddComponent<ChestRewardInteraction>();
            reward.GoldReward = 30;

            return chest;
        }

        #endregion
    }
}
