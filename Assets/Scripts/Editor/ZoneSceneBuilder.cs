using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.Core;
using CastleOfTheD20.World;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;
using CastleOfTheD20.Data;
using CastleOfTheD20.UI;
using CastleOfTheD20.Audio;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Master editor tool that constructs and connects the 7 game zones for Castle of Dice
    /// according to the master map layout (clear_map.png) and Master Specification:
    /// 1. Zone_1_VillageAndCellar (Start, Village, Shop, Cellar combat encounter)
    /// 2. Zone_2_ForestPath (Forest path with branching route and PUZZLE + GATE secret lockpick door)
    /// 3. Zone_3_CastleCourtyard (Wing 1 Boss Chamber: Cursed Commander + 1 minion)
    /// 4. Zone_4_Library (Wing 2 Boss Chamber: Shadow Mage Malakor + 1 minion)
    /// 5. Zone_5_CastleHall (Central Hall Safe Haven Hub & SavePoint.cs Rune Shrine)
    /// 6. Zone_6_Tower (Hidden Treasure Tower: Signet Ring & Giant Elixir +30 Max HP)
    /// 7. Zone_7_ThroneRoom (Wing 3 Final Boss Chamber: The Gargoyle King)
    /// Registers all 7 scenes in EditorBuildSettings.scenes.
    /// </summary>
    public static class ZoneSceneBuilder
    {
        [MenuItem("CastleOfDice/Build Zone Scenes")]
        public static void BuildAll7ZoneScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                Debug.LogWarning("[ZoneSceneBuilder] Cannot build scenes during play mode.");
                return;
            }

            Debug.Log("<color=#38bdf8><b>[ZoneSceneBuilder] === STARTING BUILD OF ALL 7 GAME ZONE SCENES ===</b></color>");

            // Ensure data assets and fantasy UI textures exist
            GenerateGameDataEditor.GenerateAllGameAssets();
            if (!System.IO.File.Exists("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png"))
            {
                GenerateFantasyUISpritesEditor.GenerateAllSprites();
            }

            // Build each zone in sequence
            BuildZone1VillageAndCellar();
            BuildZone2ForestPath();
            BuildZone3CastleCourtyard();
            BuildZone4Library();
            BuildZone5CastleHall();
            BuildZone6Tower();
            BuildZone7ThroneRoom();

            // Register all scenes into EditorBuildSettings
            RegisterScenesInBuildSettings();

            // Open Zone 1 as active scene
            EditorSceneManager.OpenScene("Assets/Scenes/Zone_1_VillageAndCellar.unity", OpenSceneMode.Single);

            Debug.Log("<color=#4ade80><b>[ZoneSceneBuilder] === ALL 7 ZONE SCENES BUILT & REGISTERED SUCCESSFULLY! ===</b></color>");
        }

        #region Zone 1: Village and Cellar

        private static void BuildZone1VillageAndCellar()
        {
            Debug.Log("[ZoneSceneBuilder] Building Zone 1: Village and Cellar...");

            // Rebuild on top of the existing Zone 1 scene (or an empty scene on first run)
            const string zone1Path = "Assets/Scenes/Zone_1_VillageAndCellar.unity";
            Scene scene = System.IO.File.Exists(zone1Path)
                ? EditorSceneManager.OpenScene(zone1Path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Ensure complete village layout & cellar
            BuildVillageEditor.BuildCompleteVillage();
            BuildCellarEditor.BuildCellarAndDoor(false);
            BuildHUDEditor.RebuildAndStyleHUD();
            BuildDialogueAndShopEditor.RebuildAndStyleDialogueAndShop();

            // Ensure Core Managers
            EnsureCoreManagers();
            MainMenuController.EnsureEventSystem();
            Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas != null) MainMenuController.EnsureGraphicRaycaster(canvas);

            // Materials & Prefabs
            Material wallMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat");
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Arch.prefab");
            GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Pillar.prefab");

            // Setup North Village Gate leading to Zone 2: Forest Path
            GameObject gateObj = GameObject.Find("Castle_Gate_Portal");
            if (gateObj == null) gateObj = GameObject.Find("Door_Village_Forest");
            if (gateObj == null)
            {
                gateObj = CreateSceneDoorway("Door_Village_Forest", new Vector3(0f, 0f, 54f), Quaternion.identity,
                    "Zone_2_ForestPath", "Spawn_From_Village", "Forest", "Enter the Whispering Woods (Forest)",
                    wallMat, archPrefab, pillarPrefab, null, true, 8f);
            }
            else
            {
                gateObj.name = "Door_Village_Forest";
                DoorTeleporter dt = gateObj.GetComponent<DoorTeleporter>();
                if (dt == null) dt = gateObj.AddComponent<DoorTeleporter>();
                dt.InitializeSceneTeleporter("Zone_2_ForestPath", "Spawn_From_Village", "Forest", "Enter the Whispering Woods (Forest)", 4.5f, true);
            }

            // Spawn point for returning from Forest
            GameObject spawnFromForest = GameObject.Find("Spawn_From_Forest");
            if (spawnFromForest == null)
            {
                spawnFromForest = CreateSpawnPoint("Spawn_From_Forest", new Vector3(0f, 0.5f, 48f), Quaternion.Euler(0f, 180f, 0f));
            }

            // Ensure Primary StartSpawn
            GameObject startSpawn = GameObject.Find("StartSpawn");
            if (startSpawn == null)
            {
                startSpawn = CreateSpawnPoint("StartSpawn", new Vector3(0f, 0.5f, -25f), Quaternion.identity);
            }

            // Save as Zone_1_VillageAndCellar.unity
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, zone1Path);
            Debug.Log("[ZoneSceneBuilder] Zone 1 saved as Assets/Scenes/Zone_1_VillageAndCellar.unity");
        }

        #endregion

        #region Zone 2: Forest Path

        private static void BuildZone2ForestPath()
        {
            Debug.Log("[ZoneSceneBuilder] Building Zone 2: Forest Path...");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Environment & Materials
            Material groundMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Fern.mat") ?? LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat");
            Material wallMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat");
            Material woodMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Logs.mat");
            Material goldMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Chest.mat");
            ItemSO swampHerb = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_SwampHerb.asset");

            GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Pine_tree.prefab");
            GameObject stumpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Stump.prefab");
            GameObject rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Rock_1.prefab");
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Arch.prefab");
            GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Pillar.prefab");

            // Setup Standard Scene Architecture (Light, Cam, EventSystem, Canvas, Managers, Player)
            SetupStandardSceneFramework(new Color(0.75f, 0.88f, 0.70f), 1.1f, new Vector3(0f, 0.5f, -44f));

            // Ground Floor (100m long path)
            GameObject ground = CreateCube("Forest_Ground", new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 110f), groundMat, null);

            // Flanking Boundary Walls & Flora
            GameObject floraRoot = new GameObject("Flora_And_Flanks");
            CreateCube("Forest_Wall_West", new Vector3(-40f, 3f, 0f), new Vector3(2f, 6f, 110f), wallMat, floraRoot.transform);
            CreateCube("Forest_Wall_East", new Vector3(40f, 3f, 0f), new Vector3(2f, 6f, 110f), wallMat, floraRoot.transform);

            // South Boundary Wall with Gate Opening
            CreateCube("Forest_Wall_South_L", new Vector3(-24f, 3f, -54f), new Vector3(32f, 6f, 2f), wallMat, floraRoot.transform);
            CreateCube("Forest_Wall_South_R", new Vector3(24f, 3f, -54f), new Vector3(32f, 6f, 2f), wallMat, floraRoot.transform);

            // North Boundary Wall with Gate Opening
            CreateCube("Forest_Wall_North_L", new Vector3(-24f, 3f, 54f), new Vector3(32f, 6f, 2f), wallMat, floraRoot.transform);
            CreateCube("Forest_Wall_North_R", new Vector3(24f, 3f, 54f), new Vector3(32f, 6f, 2f), wallMat, floraRoot.transform);

            // Scatter Trees & Rocks
            Vector3[] floraPositions = new Vector3[]
            {
                new Vector3(-18f, 0f, -35f), new Vector3(-28f, 0f, -15f), new Vector3(-20f, 0f, 10f), new Vector3(-25f, 0f, 35f),
                new Vector3(18f, 0f, -38f), new Vector3(26f, 0f, -12f), new Vector3(22f, 0f, 15f), new Vector3(28f, 0f, 38f),
                new Vector3(-12f, 0f, -10f), new Vector3(14f, 0f, 2f)
            };
            foreach (var pos in floraPositions)
            {
                if (treePrefab != null)
                {
                    GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, floraRoot.transform);
                    t.transform.position = pos;
                }
                else
                {
                    CreateCube("Pine_Tree", pos + new Vector3(0f, 3f, 0f), new Vector3(1.2f, 6f, 1.2f), woodMat, floraRoot.transform);
                }
            }

            // Mirabel's Swamp Herb Pickups
            GameObject herbContainer = new GameObject("Swamp_Herbs");
            CreateHerbPickup("SwampHerb_1", new Vector3(-12f, 0.4f, -20f), groundMat, swampHerb, herbContainer.transform);
            CreateHerbPickup("SwampHerb_2", new Vector3(15f, 0.4f, 8f), groundMat, swampHerb, herbContainer.transform);
            CreateHerbPickup("SwampHerb_3", new Vector3(-8f, 0.4f, 32f), groundMat, swampHerb, herbContainer.transform);

            // Atmospheric Lighting
            CreatePointLight("ForestLight_South", null, new Vector3(0f, 6f, -25f), new Color(0.55f, 0.85f, 0.45f), 30f, 2.0f);
            CreatePointLight("ForestLight_North", null, new Vector3(0f, 6f, 25f), new Color(0.45f, 0.75f, 0.65f), 30f, 2.0f);

            // Spawn Points
            CreateSpawnPoint("Spawn_From_Village", new Vector3(0f, 0.5f, -44f), Quaternion.identity);
            CreateSpawnPoint("Spawn_From_Courtyard", new Vector3(0f, 0.5f, 44f), Quaternion.Euler(0f, 180f, 0f));
            CreateSpawnPoint("Spawn_From_Library", new Vector3(-28f, 0.5f, 15f), Quaternion.Euler(0f, 90f, 0f));

            // South Door: Forest -> Zone 1 (Village)
            CreateSceneDoorway("Door_Forest_Village", new Vector3(0f, 0f, -53f), Quaternion.Euler(0f, 180f, 0f),
                "Zone_1_VillageAndCellar", "Spawn_From_Forest", "Village", "Return to Oakhaven Village (Village)",
                wallMat, archPrefab, pillarPrefab, null, true, 8f);

            // North Door: Forest -> Zone 3 (Castle Courtyard)
            CreateSceneDoorway("Door_Forest_Courtyard", new Vector3(0f, 0f, 53f), Quaternion.identity,
                "Zone_3_CastleCourtyard", "Spawn_From_Forest", "Courtyard", "Enter Castle Courtyard (Wing 1)",
                wallMat, archPrefab, pillarPrefab, null, true, 8f);

            // Branching West Route: PUZZLE + GATE to Zone 4 (Library)
            GameObject secretBranch = new GameObject("Branch_Puzzle_And_Gate");
            secretBranch.transform.position = new Vector3(-32f, 0f, 15f);

            // Secret Teleporter (hidden until unlocked)
            GameObject secretDoor = CreateSceneDoorway("Door_Forest_Library_Secret", new Vector3(-33f, 0f, 15f), Quaternion.Euler(0f, -90f, 0f),
                "Zone_4_Library", "Spawn_From_SecretPath", "Library", "Slip into the Grand Archives (Secret Path)",
                wallMat, archPrefab, pillarPrefab, secretBranch.transform, true, 6f);
            secretDoor.SetActive(false);

            // Locked Gate Arch blocking the passage
            GameObject puzzleGate = CreateCube("Locked_Secret_Gate", new Vector3(-25f, 2.5f, 15f), new Vector3(1.2f, 5f, 6f), wallMat, secretBranch.transform);
            LockpickInteraction lpi = puzzleGate.AddComponent<LockpickInteraction>();
            // Use reflection or serialized property setting if fields private, or standard inspector defaults
            SerializedObject soLpi = new SerializedObject(lpi);
            soLpi.FindProperty("lockpickDC").intValue = 13;
            soLpi.FindProperty("rewardGold").intValue = 25;
            soLpi.FindProperty("hiddenPathObject").objectReferenceValue = secretDoor;
            soLpi.FindProperty("hasTrap").boolValue = true;
            soLpi.FindProperty("trapDamage").intValue = 4;
            soLpi.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Zone_2_ForestPath.unity");
            Debug.Log("[ZoneSceneBuilder] Zone 2 saved as Assets/Scenes/Zone_2_ForestPath.unity");
        }

        #endregion

        #region Zone 3: Castle Courtyard

        private static void BuildZone3CastleCourtyard()
        {
            Debug.Log("[ZoneSceneBuilder] Building Zone 3: Castle Courtyard...");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material wallMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat");
            Material woodMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Logs.mat");
            Material metalMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_HammerJAanvil.mat");
            Material goldMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Chest.mat");

            GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Pillar.prefab");
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Arch.prefab");
            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Chest.prefab");
            GameObject combatGridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/CombatGrid.prefab");

            ItemSO signetRing = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_SignetRing.asset");

            // Setup Framework
            SetupStandardSceneFramework(new Color(1f, 0.95f, 0.85f), 1.2f, new Vector3(0f, 0.5f, -42f));

            // Courtyard Floor (100 x 100)
            CreateCube("Courtyard_Floor", new Vector3(0f, -0.5f, 0f), new Vector3(90f, 1f, 90f), wallMat, null);

            // Perimeter Walls
            GameObject walls = new GameObject("Walls");
            CreateCube("Courtyard_Wall_West", new Vector3(-45f, 4f, 0f), new Vector3(2f, 8f, 90f), wallMat, walls.transform);
            CreateCube("Courtyard_Wall_East", new Vector3(45f, 4f, 0f), new Vector3(2f, 8f, 90f), wallMat, walls.transform);
            CreateCube("Courtyard_Wall_South_L", new Vector3(-25f, 4f, -45f), new Vector3(40f, 8f, 2f), wallMat, walls.transform);
            CreateCube("Courtyard_Wall_South_R", new Vector3(25f, 4f, -45f), new Vector3(40f, 8f, 2f), wallMat, walls.transform);
            CreateCube("Courtyard_Wall_North_L", new Vector3(-25f, 4f, 45f), new Vector3(40f, 8f, 2f), wallMat, walls.transform);
            CreateCube("Courtyard_Wall_North_R", new Vector3(25f, 4f, 45f), new Vector3(40f, 8f, 2f), wallMat, walls.transform);

            CreateCornerPillars("Courtyard_Pillar", Vector3.zero, 42f, 42f, 8f, wallMat, pillarPrefab, walls.transform);

            // Lighting
            CreatePointLight("CourtyardLight_Center", null, new Vector3(0f, 7f, 0f), new Color(1f, 0.75f, 0.45f), 35f, 2.5f);
            CreatePointLight("CourtyardLight_North", null, new Vector3(0f, 6f, 25f), new Color(1f, 0.65f, 0.35f), 28f, 2.0f);

            // Spawn Points
            CreateSpawnPoint("Spawn_From_Forest", new Vector3(0f, 0.5f, -40f), Quaternion.identity);
            CreateSpawnPoint("Spawn_From_Hall", new Vector3(0f, 0.5f, 40f), Quaternion.Euler(0f, 180f, 0f));

            // South Door: Courtyard -> Zone 2 (Forest)
            GameObject doorSouth = CreateSceneDoorway("Door_Courtyard_Forest", new Vector3(0f, 0f, -44.5f), Quaternion.Euler(0f, 180f, 0f),
                "Zone_2_ForestPath", "Spawn_From_Courtyard", "Forest", "Return to Forest Path (Forest)",
                wallMat, archPrefab, pillarPrefab, null, true, 8f);

            // North Door: Courtyard -> Zone 5 (Castle Central Hall)
            GameObject doorNorth = CreateSceneDoorway("Door_Courtyard_CastleHall", new Vector3(0f, 0f, 44.5f), Quaternion.identity,
                "Zone_5_CastleHall", "Spawn_From_Courtyard", "CastleHall", "Enter Great Central Hall (Safe Haven Hub)",
                wallMat, archPrefab, pillarPrefab, null, true, 8f);

            // Exit Barriers (lock during encounter)
            GameObject barrierSouth = CreateCube("Barrier_South", new Vector3(0f, 3.5f, -44f), new Vector3(10f, 7f, 1.5f), metalMat, null);
            barrierSouth.SetActive(false);
            GameObject barrierNorth = CreateCube("Barrier_North", new Vector3(0f, 3.5f, 44f), new Vector3(10f, 7f, 1.5f), metalMat, null);
            barrierNorth.SetActive(false);

            // Tactical Grid
            if (combatGridPrefab != null)
            {
                GameObject grid = (GameObject)PrefabUtility.InstantiatePrefab(combatGridPrefab);
                grid.name = "CombatGrid_Courtyard";
                grid.transform.position = new Vector3(0f, 0.05f, 10f);
            }

            // Enemies: 1 Skeleton minion + Boss: Cursed Commander (Solo hero balance: max 1-2 enemies!)
            GameObject enemiesRoot = new GameObject("Enemies");
            List<GameObject> enemyList = new List<GameObject>();

            GameObject skel = CreateEnemyUnit("Courtyard_Skeleton", new Vector3(-6f, 0.9f, 12f), woodMat, metalMat, enemiesRoot.transform, 20, 12, 4);
            skel.SetActive(false);
            enemyList.Add(skel);

            GameObject bossObj = CreateBossUnit<CursedCommanderBoss>("Boss_CursedCommander", new Vector3(0f, 1.2f, 18f), metalMat, goldMat, enemiesRoot.transform);
            bossObj.SetActive(false);
            enemyList.Add(bossObj);

            // Pre-combat Dialogue NPC
            CreateBossDialogueNPC("NPC_CursedCommander", new Vector3(0f, 1.0f, -12f), metalMat, "Assets/Data/Dialogues/Commander_Intro.asset", "Challenge the Cursed Commander", null);

            // Reward Chest
            GameObject chestObj = CreateRewardChest("Courtyard_Reward_Chest", new Vector3(0f, 0.4f, 30f), woodMat, goldMat, chestPrefab, null, 40, signetRing);
            chestObj.SetActive(false);

            // Encounter Controller Trigger
            GameObject triggerObj = new GameObject("Courtyard_Encounter_Trigger");
            triggerObj.transform.position = new Vector3(0f, 2.5f, 10f);
            BoxCollider trigCol = triggerObj.AddComponent<BoxCollider>();
            trigCol.size = new Vector3(32f, 6f, 32f);
            trigCol.isTrigger = true;

            DungeonRoomController room = triggerObj.AddComponent<DungeonRoomController>();
            room.roomLocation = "Courtyard";
            room.bossIdentifier = "CursedCommander";
            room.roomEnemies = enemyList;
            room.secretPassageOrChest = chestObj;
            room.exitBarriers = new List<GameObject> { barrierSouth, barrierNorth };

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Zone_3_CastleCourtyard.unity");
            Debug.Log("[ZoneSceneBuilder] Zone 3 saved as Assets/Scenes/Zone_3_CastleCourtyard.unity");
        }

        #endregion

        #region Zone 4: Library

        private static void BuildZone4Library()
        {
            Debug.Log("[ZoneSceneBuilder] Building Zone 4: Arcane Library...");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material wallMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat");
            Material woodMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Logs.mat");
            Material metalMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_HammerJAanvil.mat");
            Material goldMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Chest.mat");

            GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Pillar.prefab");
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Arch.prefab");
            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Chest.prefab");
            GameObject combatGridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/CombatGrid.prefab");

            ItemSO greaterPotion = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_GreaterPotion.asset");

            // Setup Framework
            SetupStandardSceneFramework(new Color(0.35f, 0.55f, 0.95f), 1.0f, new Vector3(-35f, 0.5f, 0f));

            // Library Floor (90 x 90)
            CreateCube("Library_Floor", new Vector3(0f, -0.5f, 0f), new Vector3(90f, 1f, 90f), wallMat, null);

            // Perimeter Walls
            GameObject walls = new GameObject("Walls");
            CreateCube("Library_Wall_North", new Vector3(0f, 4f, 45f), new Vector3(90f, 8f, 2f), wallMat, walls.transform);
            CreateCube("Library_Wall_South", new Vector3(0f, 4f, -45f), new Vector3(90f, 8f, 2f), wallMat, walls.transform);
            CreateCube("Library_Wall_West_L", new Vector3(-45f, 4f, -25f), new Vector3(2f, 8f, 40f), wallMat, walls.transform);
            CreateCube("Library_Wall_West_R", new Vector3(-45f, 4f, 25f), new Vector3(2f, 8f, 40f), wallMat, walls.transform);
            CreateCube("Library_Wall_East_L", new Vector3(45f, 4f, -25f), new Vector3(2f, 8f, 40f), wallMat, walls.transform);
            CreateCube("Library_Wall_East_R", new Vector3(45f, 4f, 25f), new Vector3(2f, 8f, 40f), wallMat, walls.transform);

            // Decorative Pillars & Bookshelves
            CreateCornerPillars("Library_Pillar", Vector3.zero, 40f, 40f, 8f, wallMat, pillarPrefab, walls.transform);
            CreateCube("Bookshelf_1", new Vector3(-20f, 3.5f, 25f), new Vector3(12f, 7f, 2.5f), woodMat, walls.transform);
            CreateCube("Bookshelf_2", new Vector3(20f, 3.5f, 25f), new Vector3(12f, 7f, 2.5f), woodMat, walls.transform);
            CreateCube("Bookshelf_3", new Vector3(-20f, 3.5f, -25f), new Vector3(12f, 7f, 2.5f), woodMat, walls.transform);
            CreateCube("Bookshelf_4", new Vector3(20f, 3.5f, -25f), new Vector3(12f, 7f, 2.5f), woodMat, walls.transform);

            // Arcane Blue Lighting
            CreatePointLight("ArcaneLight_Center", null, new Vector3(0f, 6.5f, 0f), new Color(0.25f, 0.55f, 1.0f), 32f, 2.5f);
            CreatePointLight("ArcaneLight_East", null, new Vector3(20f, 5.0f, 15f), new Color(0.45f, 0.25f, 0.95f), 22f, 2.0f);
            CreatePointLight("ArcaneLight_West", null, new Vector3(-20f, 5.0f, 15f), new Color(0.45f, 0.25f, 0.95f), 22f, 2.0f);

            // Spawn Points
            CreateSpawnPoint("Spawn_From_SecretPath", new Vector3(-38f, 0.5f, 0f), Quaternion.Euler(0f, 90f, 0f));
            CreateSpawnPoint("Spawn_From_Hall", new Vector3(38f, 0.5f, 0f), Quaternion.Euler(0f, -90f, 0f));

            // West Door: Secret passage to Zone 2 (Forest Path)
            CreateSceneDoorway("Door_Library_SecretPath", new Vector3(-44.5f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f),
                "Zone_2_ForestPath", "Spawn_From_Library", "Forest", "Slip through Secret Passage to Whispering Woods",
                wallMat, archPrefab, pillarPrefab, null, true, 6f);

            // East Door: Library -> Zone 5 (Castle Central Hall)
            CreateSceneDoorway("Door_Library_CastleHall", new Vector3(44.5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f),
                "Zone_5_CastleHall", "Spawn_From_Library", "CastleHall", "Enter Great Central Hall (Safe Haven Hub)",
                wallMat, archPrefab, pillarPrefab, null, true, 6f);

            // Exit Barriers
            GameObject barrierWest = CreateCube("Barrier_West", new Vector3(-44f, 3.5f, 0f), new Vector3(1.5f, 7f, 8f), metalMat, null);
            barrierWest.SetActive(false);
            GameObject barrierEast = CreateCube("Barrier_East", new Vector3(44f, 3.5f, 0f), new Vector3(1.5f, 7f, 8f), metalMat, null);
            barrierEast.SetActive(false);

            // Tactical Grid
            if (combatGridPrefab != null)
            {
                GameObject grid = (GameObject)PrefabUtility.InstantiatePrefab(combatGridPrefab);
                grid.name = "CombatGrid_Library";
                grid.transform.position = new Vector3(0f, 0.05f, 10f);
            }

            // Enemies: 1 Shadow Decoy + Boss: Shadow Mage Malakor (Max 1-2 enemies!)
            GameObject enemiesRoot = new GameObject("Enemies");
            List<GameObject> enemyList = new List<GameObject>();

            GameObject decoy = CreateEnemyUnit("Shadow_Decoy", new Vector3(6f, 0.9f, 12f), wallMat, metalMat, enemiesRoot.transform, 18, 12, 4);
            decoy.SetActive(false);
            enemyList.Add(decoy);

            GameObject bossObj = CreateBossUnit<ShadowMageMalakorBoss>("Boss_ShadowMageMalakor", new Vector3(0f, 1.1f, 18f), wallMat, metalMat, enemiesRoot.transform);
            bossObj.SetActive(false);
            enemyList.Add(bossObj);

            // Pre-combat Dialogue NPC
            CreateBossDialogueNPC("NPC_Malakor", new Vector3(0f, 1.0f, -12f), metalMat, "Assets/Data/Dialogues/Malakor_Intro.asset", "Challenge Shadow Mage Malakor", null);

            // Reward Chest
            GameObject chestObj = CreateRewardChest("Library_Reward_Chest", new Vector3(0f, 0.4f, 30f), woodMat, goldMat, chestPrefab, null, 60, greaterPotion);
            chestObj.SetActive(false);

            // Encounter Controller Trigger
            GameObject triggerObj = new GameObject("Library_Encounter_Trigger");
            triggerObj.transform.position = new Vector3(0f, 2.5f, 10f);
            BoxCollider trigCol = triggerObj.AddComponent<BoxCollider>();
            trigCol.size = new Vector3(32f, 6f, 32f);
            trigCol.isTrigger = true;

            DungeonRoomController room = triggerObj.AddComponent<DungeonRoomController>();
            room.roomLocation = "Library";
            room.bossIdentifier = "ShadowMageMalakor";
            room.roomEnemies = enemyList;
            room.secretPassageOrChest = chestObj;
            room.exitBarriers = new List<GameObject> { barrierWest, barrierEast };

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Zone_4_Library.unity");
            Debug.Log("[ZoneSceneBuilder] Zone 4 saved as Assets/Scenes/Zone_4_Library.unity");
        }

        #endregion

        #region Zone 5: Castle Central Hall (Safe Haven Hub)

        private static void BuildZone5CastleHall()
        {
            Debug.Log("[ZoneSceneBuilder] Building Zone 5: Castle Central Hall (Safe Haven Hub)...");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material wallMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat");
            Material goldMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Chest.mat");
            Material stoneMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Rocks_1_2.mat") ?? wallMat;

            GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Pillar.prefab");
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Arch.prefab");
            GameObject altarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Altar.prefab");

            // Setup Framework
            SetupStandardSceneFramework(new Color(1f, 0.88f, 0.65f), 1.25f, new Vector3(0f, 0.5f, -32f));

            // Central Hall Floor (80 x 80)
            CreateCube("CentralHall_Floor", new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), wallMat, null);

            // Perimeter Walls with 4 Openings (South, West, East, North)
            GameObject walls = new GameObject("Walls");
            // South wall
            CreateCube("Hall_Wall_South_L", new Vector3(-24f, 4f, -40f), new Vector3(32f, 8f, 2f), wallMat, walls.transform);
            CreateCube("Hall_Wall_South_R", new Vector3(24f, 4f, -40f), new Vector3(32f, 8f, 2f), wallMat, walls.transform);
            // North wall
            CreateCube("Hall_Wall_North_L", new Vector3(-24f, 4f, 40f), new Vector3(32f, 8f, 2f), wallMat, walls.transform);
            CreateCube("Hall_Wall_North_R", new Vector3(24f, 4f, 40f), new Vector3(32f, 8f, 2f), wallMat, walls.transform);
            // West wall
            CreateCube("Hall_Wall_West_S", new Vector3(-40f, 4f, -24f), new Vector3(2f, 8f, 32f), wallMat, walls.transform);
            CreateCube("Hall_Wall_West_N", new Vector3(-40f, 4f, 24f), new Vector3(2f, 8f, 32f), wallMat, walls.transform);
            // East wall
            CreateCube("Hall_Wall_East_S", new Vector3(40f, 4f, -24f), new Vector3(2f, 8f, 32f), wallMat, walls.transform);
            CreateCube("Hall_Wall_East_N", new Vector3(40f, 4f, 24f), new Vector3(2f, 8f, 32f), wallMat, walls.transform);

            // Vaulted Grand Columns
            CreateCornerPillars("Hall_Pillar", Vector3.zero, 35f, 35f, 8f, wallMat, pillarPrefab, walls.transform);
            CreateCornerPillars("Hall_Inner_Col", Vector3.zero, 18f, 18f, 8f, wallMat, pillarPrefab, walls.transform);

            // Warm Sanctuary Lighting
            CreatePointLight("SanctuaryLight_Center", null, new Vector3(0f, 7.5f, 0f), new Color(1f, 0.90f, 0.60f), 35f, 3.0f);
            CreatePointLight("SanctuaryLight_South", null, new Vector3(0f, 6.0f, -20f), new Color(1f, 0.80f, 0.50f), 24f, 2.0f);
            CreatePointLight("SanctuaryLight_North", null, new Vector3(0f, 6.0f, 20f), new Color(1f, 0.80f, 0.50f), 24f, 2.0f);

            // Safe Haven Feature: Runestone Shrine (SavePoint.cs)
            GameObject shrineObj;
            if (altarPrefab != null)
            {
                shrineObj = (GameObject)PrefabUtility.InstantiatePrefab(altarPrefab);
                shrineObj.name = "Rune_Save_Shrine";
                shrineObj.transform.position = Vector3.zero;
            }
            else
            {
                shrineObj = CreateCube("Rune_Save_Shrine", Vector3.zero, new Vector3(3f, 1.2f, 3f), stoneMat, null);
            }
            SavePoint sp = shrineObj.GetComponent<SavePoint>();
            if (sp == null) sp = shrineObj.AddComponent<SavePoint>();
            BoxCollider spCol = shrineObj.GetComponent<BoxCollider>();
            if (spCol == null) spCol = shrineObj.AddComponent<BoxCollider>();
            spCol.size = new Vector3(3f, 2.5f, 3f);
            spCol.center = new Vector3(0f, 1.2f, 0f);

            // Cyan Rune Crystal on top
            GameObject crystal = CreateCube("Rune_Crystal", new Vector3(0f, 1.4f, 0f), new Vector3(0.6f, 1.2f, 0.6f), goldMat, shrineObj.transform);
            crystal.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            CreatePointLight("ShrineGlow", shrineObj.transform, new Vector3(0f, 2.2f, 0f), new Color(0.2f, 0.9f, 1.0f), 14f, 2.5f);

            // Spawn Points
            CreateSpawnPoint("Spawn_From_Courtyard", new Vector3(0f, 0.5f, -34f), Quaternion.identity);
            CreateSpawnPoint("Spawn_From_Library", new Vector3(-34f, 0.5f, 0f), Quaternion.Euler(0f, 90f, 0f));
            CreateSpawnPoint("Spawn_From_Tower", new Vector3(34f, 0.5f, 0f), Quaternion.Euler(0f, -90f, 0f));
            CreateSpawnPoint("Spawn_From_ThroneRoom", new Vector3(0f, 0.5f, 34f), Quaternion.Euler(0f, 180f, 0f));

            // Connecting Doors (4 Doors)
            // 1. South Door: to Zone 3 (Castle Courtyard)
            CreateSceneDoorway("Door_Hall_Courtyard", new Vector3(0f, 0f, -39.5f), Quaternion.Euler(0f, 180f, 0f),
                "Zone_3_CastleCourtyard", "Spawn_From_Hall", "Courtyard", "Return to Courtyard (Wing 1)",
                wallMat, archPrefab, pillarPrefab, null, true, 8f);

            // 2. West Door: to Zone 4 (Library)
            CreateSceneDoorway("Door_Hall_Library", new Vector3(-39.5f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f),
                "Zone_4_Library", "Spawn_From_Hall", "Library", "Enter Arcane Library (Wing 2)",
                wallMat, archPrefab, pillarPrefab, null, true, 8f);

            // 3. East Door: to Zone 6 (Hidden Treasure Tower)
            CreateSceneDoorway("Door_Hall_Tower", new Vector3(39.5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f),
                "Zone_6_Tower", "Spawn_From_Hall", "Tower", "Ascend Spiral Stairs into Treasure Tower",
                wallMat, archPrefab, pillarPrefab, null, true, 8f);

            // 4. North Gate: to Zone 7 (Throne Room / Gargoyle King)
            CreateSceneDoorway("Door_Hall_ThroneRoom", new Vector3(0f, 0f, 39.5f), Quaternion.identity,
                "Zone_7_ThroneRoom", "Spawn_From_Hall", "CrownHall", "Confront Final Boss in Crown Hall (Wing 3)",
                wallMat, archPrefab, pillarPrefab, null, true, 8f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Zone_5_CastleHall.unity");
            Debug.Log("[ZoneSceneBuilder] Zone 5 saved as Assets/Scenes/Zone_5_CastleHall.unity");
        }

        #endregion

        #region Zone 6: Hidden Treasure Tower

        private static void BuildZone6Tower()
        {
            Debug.Log("[ZoneSceneBuilder] Building Zone 6: Hidden Treasure Tower...");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material wallMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat");
            Material woodMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Logs.mat");
            Material goldMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Chest.mat");
            Material stoneMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Rocks_1_2.mat") ?? wallMat;

            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Chest.prefab");
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Arch.prefab");
            GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Pillar.prefab");
            ItemSO signetRing = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_SignetRing.asset");

            // Setup Framework
            SetupStandardSceneFramework(new Color(0.45f, 0.65f, 1f), 1.1f, new Vector3(0f, 0.5f, -9f));

            // Tower Floor (Octagonal Spire Chamber, 30m)
            CreateCube("Tower_Floor", new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f), wallMat, null);

            // Circular/Octagonal Perimeter Walls
            GameObject walls = new GameObject("Walls");
            CreateCube("Tower_Wall_North", new Vector3(0f, 4f, 15f), new Vector3(16f, 8f, 2f), wallMat, walls.transform);
            CreateCube("Tower_Wall_East", new Vector3(15f, 4f, 0f), new Vector3(2f, 8f, 16f), wallMat, walls.transform);
            CreateCube("Tower_Wall_West", new Vector3(-15f, 4f, 0f), new Vector3(2f, 8f, 16f), wallMat, walls.transform);
            CreateCube("Tower_Wall_South_L", new Vector3(-10f, 4f, -15f), new Vector3(10f, 8f, 2f), wallMat, walls.transform);
            CreateCube("Tower_Wall_South_R", new Vector3(10f, 4f, -15f), new Vector3(10f, 8f, 2f), wallMat, walls.transform);

            // Diagonal corner braces
            GameObject dNE = CreateCube("Wall_NE", new Vector3(10.6f, 4f, 10.6f), new Vector3(2f, 8f, 10f), wallMat, walls.transform);
            dNE.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            GameObject dNW = CreateCube("Wall_NW", new Vector3(-10.6f, 4f, 10.6f), new Vector3(2f, 8f, 10f), wallMat, walls.transform);
            dNW.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
            GameObject dSE = CreateCube("Wall_SE", new Vector3(10.6f, 4f, -10.6f), new Vector3(2f, 8f, 10f), wallMat, walls.transform);
            dSE.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
            GameObject dSW = CreateCube("Wall_SW", new Vector3(-10.6f, 4f, -10.6f), new Vector3(2f, 8f, 10f), wallMat, walls.transform);
            dSW.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            // Starlight / Celestial Lighting
            CreatePointLight("TowerLight_Center", null, new Vector3(0f, 6.5f, 0f), new Color(0.55f, 0.85f, 1.0f), 24f, 2.5f);

            // Spawn Point
            CreateSpawnPoint("Spawn_From_Hall", new Vector3(0f, 0.5f, -9f), Quaternion.identity);

            // Doorway back to Castle Hall
            CreateSceneDoorway("Door_Tower_CastleHall", new Vector3(0f, 0f, -14.5f), Quaternion.Euler(0f, 180f, 0f),
                "Zone_5_CastleHall", "Spawn_From_Tower", "CastleHall", "Return to Great Central Hall",
                wallMat, archPrefab, pillarPrefab, null, true, 6f);

            // Reward 1: Treasure Chest containing Othelia's Signet Ring + 50 Gold
            CreateRewardChest("Tower_Reward_Chest", new Vector3(-5f, 0.4f, 5f), woodMat, goldMat, chestPrefab, null, 50, signetRing);

            // Reward 2: Giant's Elixir Altar (+30 Max HP permanently!)
            GameObject elixirPedestal = CreateCube("GiantElixir_Pedestal", new Vector3(5f, 0.5f, 5f), new Vector3(1.5f, 1.0f, 1.5f), stoneMat, null);
            GiantElixirInteraction elixir = elixirPedestal.AddComponent<GiantElixirInteraction>();
            BoxCollider elixirCol = elixirPedestal.GetComponent<BoxCollider>();
            elixirCol.size = new Vector3(2.5f, 2.0f, 2.5f);

            // Golden visual bottle
            GameObject bottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bottle.name = "Elixir_Vial";
            bottle.transform.SetParent(elixirPedestal.transform, false);
            bottle.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            bottle.transform.localScale = new Vector3(0.4f, 0.5f, 0.4f);
            if (goldMat != null) bottle.GetComponent<Renderer>().sharedMaterial = goldMat;
            CreatePointLight("ElixirGlow", elixirPedestal.transform, new Vector3(0f, 1.5f, 0f), new Color(1.0f, 0.85f, 0.2f), 8f, 2.0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Zone_6_Tower.unity");
            Debug.Log("[ZoneSceneBuilder] Zone 6 saved as Assets/Scenes/Zone_6_Tower.unity");
        }

        #endregion

        #region Zone 7: Throne Room (Final Boss)

        private static void BuildZone7ThroneRoom()
        {
            Debug.Log("[ZoneSceneBuilder] Building Zone 7: Throne Room (Wing 3 Final Boss)...");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material wallMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat");
            Material woodMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Logs.mat");
            Material metalMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_HammerJAanvil.mat");
            Material goldMat = LoadMaterial("Assets/LowPolyVillageAll/Omat Materials/M_Chest.mat");

            GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Pillar.prefab");
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Arch.prefab");
            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Chest.prefab");
            GameObject combatGridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/CombatGrid.prefab");

            // Setup Framework
            SetupStandardSceneFramework(new Color(1f, 0.40f, 0.20f), 1.3f, new Vector3(0f, 0.5f, -42f));

            // Throne Room Floor (100 x 100)
            CreateCube("ThroneRoom_Floor", new Vector3(0f, -0.5f, 0f), new Vector3(100f, 1f, 100f), wallMat, null);

            // Perimeter Walls
            GameObject walls = new GameObject("Walls");
            CreateCube("ThroneRoom_Wall_North", new Vector3(0f, 4.5f, 50f), new Vector3(100f, 9f, 2f), wallMat, walls.transform);
            CreateCube("ThroneRoom_Wall_West", new Vector3(-50f, 4.5f, 0f), new Vector3(2f, 9f, 100f), wallMat, walls.transform);
            CreateCube("ThroneRoom_Wall_East", new Vector3(50f, 4.5f, 0f), new Vector3(2f, 9f, 100f), wallMat, walls.transform);
            CreateCube("ThroneRoom_Wall_South_L", new Vector3(-28f, 4.5f, -50f), new Vector3(44f, 9f, 2f), wallMat, walls.transform);
            CreateCube("ThroneRoom_Wall_South_R", new Vector3(28f, 4.5f, -50f), new Vector3(44f, 9f, 2f), wallMat, walls.transform);

            // Throne Dais Platform
            CreateCube("Throne_Dais", new Vector3(0f, 0.6f, 32f), new Vector3(16f, 1.2f, 10f), wallMat, null);
            CreateCube("Throne_Col_L", new Vector3(-7f, 4.5f, 32f), new Vector3(2.0f, 9f, 2.0f), wallMat, null);
            CreateCube("Throne_Col_R", new Vector3(7f, 4.5f, 32f), new Vector3(2.0f, 9f, 2.0f), wallMat, null);
            CreateCornerPillars("Throne_Pillar", Vector3.zero, 45f, 45f, 9f, wallMat, pillarPrefab, walls.transform);

            // Majestic Crimson & Gold Lighting
            CreatePointLight("CrownLight_Center", null, new Vector3(0f, 7.5f, 10f), new Color(1f, 0.40f, 0.15f), 35f, 3.0f);
            CreatePointLight("CrownLight_Throne", null, new Vector3(0f, 6.0f, 32f), new Color(1f, 0.85f, 0.25f), 28f, 2.5f);

            // Spawn Point
            CreateSpawnPoint("Spawn_From_Hall", new Vector3(0f, 0.5f, -42f), Quaternion.identity);

            // South Doorway: returns to Zone 5 (Castle Central Hall)
            CreateSceneDoorway("Door_ThroneRoom_CastleHall", new Vector3(0f, 0f, -49.5f), Quaternion.Euler(0f, 180f, 0f),
                "Zone_5_CastleHall", "Spawn_From_ThroneRoom", "CastleHall", "Return to Great Central Hall (Hub)",
                wallMat, archPrefab, pillarPrefab, null, true, 8f);

            // Exit Barrier (locked during boss combat)
            GameObject barrierSouth = CreateCube("Barrier_South", new Vector3(0f, 4f, -49f), new Vector3(12f, 8f, 1.5f), metalMat, null);
            barrierSouth.SetActive(false);

            // Tactical Grid (12x12)
            if (combatGridPrefab != null)
            {
                GameObject grid = (GameObject)PrefabUtility.InstantiatePrefab(combatGridPrefab);
                grid.name = "CombatGrid_CrownHall";
                grid.transform.position = new Vector3(0f, 0.05f, 12f);
            }

            // Boss: The Gargoyle King (Wing 3 Final Boss)
            GameObject enemiesRoot = new GameObject("Enemies");
            List<GameObject> enemyList = new List<GameObject>();

            GameObject bossObj = CreateBossUnit<GargoyleKingBoss>("Boss_GargoyleKing", new Vector3(0f, 1.8f, 28f), wallMat, goldMat, enemiesRoot.transform);
            bossObj.SetActive(false);
            enemyList.Add(bossObj);

            // Pre-combat Dialogue NPC
            CreateBossDialogueNPC("NPC_GargoyleKing", new Vector3(0f, 1.2f, -10f), wallMat, "Assets/Data/Dialogues/GargoyleKing_Intro.asset", "Challenge the Gargoyle King", null);

            // Royal Treasure Chest (100 Gold + Campaign Victory!)
            GameObject chestObj = CreateRewardChest("CrownHall_Treasure_Chest", new Vector3(0f, 1.4f, 34f), woodMat, goldMat, chestPrefab, null, 100, null);
            chestObj.SetActive(false);

            // Encounter Controller Trigger
            GameObject triggerObj = new GameObject("CrownHall_Encounter_Trigger");
            triggerObj.transform.position = new Vector3(0f, 2.5f, 15f);
            BoxCollider trigCol = triggerObj.AddComponent<BoxCollider>();
            trigCol.size = new Vector3(36f, 6f, 36f);
            trigCol.isTrigger = true;

            DungeonRoomController room = triggerObj.AddComponent<DungeonRoomController>();
            room.roomLocation = "CrownHall";
            room.bossIdentifier = "GargoyleKing";
            room.roomEnemies = enemyList;
            room.secretPassageOrChest = chestObj;
            room.exitBarriers = new List<GameObject> { barrierSouth };

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Zone_7_ThroneRoom.unity");
            Debug.Log("[ZoneSceneBuilder] Zone 7 saved as Assets/Scenes/Zone_7_ThroneRoom.unity");
        }

        #endregion

        #region Framework & Setup Helpers

        private static void SetupStandardSceneFramework(Color lightColor, float lightIntensity, Vector3 playerSpawnPos)
        {
            // 1. Directional Light
            GameObject dirLight = new GameObject("Directional Light");
            Light l = dirLight.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = lightColor;
            l.intensity = lightIntensity;
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. Camera with CameraFollow
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camObj.AddComponent<AudioListener>();
            CameraFollow follow = camObj.AddComponent<CameraFollow>();
            follow.Offset = new Vector3(-6f, 12f, -12f);
            follow.SmoothTime = 0.2f;
            camObj.transform.position = playerSpawnPos + new Vector3(-6f, 12f, -12f);
            camObj.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            // 3. EventSystem with InputSystemUIInputModule
            MainMenuController.EnsureEventSystem();

            // 4. Managers
            EnsureCoreManagers();

            // 5. Player Hero
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Players/PlayerHero.prefab");
            if (playerPrefab != null)
            {
                GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.name = "PlayerHero";
                player.transform.position = playerSpawnPos;

                CapsuleCollider ccCol = player.GetComponent<CapsuleCollider>();
                if (ccCol != null) Object.DestroyImmediate(ccCol);

                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

                SerializedObject soCam = new SerializedObject(follow);
                soCam.FindProperty("target").objectReferenceValue = player.transform;
                soCam.ApplyModifiedProperties();
            }

            // 6. UI EventSystem and Raycaster
            Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas != null) MainMenuController.EnsureGraphicRaycaster(canvas);
        }

        private static void EnsureCoreManagers()
        {
            GameObject managersObj = GameObject.Find("Managers");
            if (managersObj == null) managersObj = new GameObject("Managers");
            if (managersObj.GetComponent<GameManager>() == null) managersObj.AddComponent<GameManager>();
            if (managersObj.GetComponent<SceneLoader>() == null) managersObj.AddComponent<SceneLoader>();
            if (managersObj.GetComponent<MusicManager>() == null) managersObj.AddComponent<MusicManager>();
            if (managersObj.GetComponent<SFXManager>() == null) managersObj.AddComponent<SFXManager>();
            if (managersObj.GetComponent<InventoryManager>() == null) managersObj.AddComponent<InventoryManager>();
            if (managersObj.GetComponent<PlayerProgressionManager>() == null) managersObj.AddComponent<PlayerProgressionManager>();
            if (managersObj.GetComponent<QuestManager>() == null) managersObj.AddComponent<QuestManager>();
            if (managersObj.GetComponent<FloatingCombatText>() == null) managersObj.AddComponent<FloatingCombatText>();

            AssignQuestDatabase(managersObj.GetComponent<QuestManager>());
        }

        /// <summary>
        /// Serializes every QuestSO into the QuestManager so quests exist in player builds
        /// (AssetDatabase is editor-only).
        /// </summary>
        private static void AssignQuestDatabase(QuestManager questManager)
        {
            if (questManager == null) return;

            string[] guids = AssetDatabase.FindAssets("t:QuestSO", new[] { "Assets/Data" });
            SerializedObject so = new SerializedObject(questManager);
            SerializedProperty list = so.FindProperty("questDatabase");
            list.ClearArray();
            for (int i = 0; i < guids.Length; i++)
            {
                QuestSO quest = AssetDatabase.LoadAssetAtPath<QuestSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (quest == null) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = quest;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateSceneDoorway(string name, Vector3 pos, Quaternion rot,
            string targetScene, string targetSpawn, string destZone, string prompt,
            Material wallMat, GameObject archPrefab, GameObject pillarPrefab, Transform parent,
            bool walkTrigger = true, float width = 6f)
        {
            GameObject doorObj = new GameObject(name);
            if (parent != null) doorObj.transform.SetParent(parent, false);
            doorObj.transform.position = pos;
            doorObj.transform.rotation = rot;

            if (archPrefab != null)
            {
                GameObject arch = (GameObject)PrefabUtility.InstantiatePrefab(archPrefab, doorObj.transform);
                arch.name = "Arch_Model";
                arch.transform.localPosition = Vector3.zero;
                arch.transform.localRotation = Quaternion.identity;
                arch.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
            }
            else
            {
                CreateCube("Arch_Pillar_L", pos + rot * new Vector3(-width * 0.5f, 2.5f, 0f), new Vector3(1.5f, 5f, 1.5f), wallMat, doorObj.transform);
                CreateCube("Arch_Pillar_R", pos + rot * new Vector3(width * 0.5f, 2.5f, 0f), new Vector3(1.5f, 5f, 1.5f), wallMat, doorObj.transform);
                CreateCube("Arch_Header", pos + rot * new Vector3(0f, 5.2f, 0f), new Vector3(width + 1.5f, 1.0f, 1.5f), wallMat, doorObj.transform);
            }

            if (pillarPrefab != null)
            {
                GameObject pL = (GameObject)PrefabUtility.InstantiatePrefab(pillarPrefab, doorObj.transform);
                pL.name = "DecoPillar_L";
                pL.transform.localPosition = new Vector3(-width * 0.55f, 0f, 0f);
                GameObject pR = (GameObject)PrefabUtility.InstantiatePrefab(pillarPrefab, doorObj.transform);
                pR.name = "DecoPillar_R";
                pR.transform.localPosition = new Vector3(width * 0.55f, 0f, 0f);
            }

            // Interactive DoorTeleporter Trigger
            GameObject triggerObj = new GameObject(name + "_Trigger");
            triggerObj.transform.SetParent(doorObj.transform, false);
            triggerObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            BoxCollider triggerCol = triggerObj.AddComponent<BoxCollider>();
            triggerCol.size = new Vector3(width, 4.0f, 3.0f);
            triggerCol.isTrigger = true;

            DoorTeleporter dt = triggerObj.AddComponent<DoorTeleporter>();
            dt.InitializeSceneTeleporter(targetScene, targetSpawn, destZone, prompt, 4.5f, walkTrigger);

            return doorObj;
        }

        private static GameObject CreateSpawnPoint(string name, Vector3 pos, Quaternion rot, Transform parent = null)
        {
            GameObject spawnObj = new GameObject(name);
            if (parent != null) spawnObj.transform.SetParent(parent, false);
            spawnObj.transform.position = pos;
            spawnObj.transform.rotation = rot;

            spawnObj.AddComponent<StartSpawnPoint>();
            BoxCollider col = spawnObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2f, 2f, 2f);
            return spawnObj;
        }

        private static GameObject CreateCube(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            if (parent != null) obj.transform.SetParent(parent, false);
            obj.transform.position = pos;
            obj.transform.localScale = scale;
            if (mat != null)
            {
                Renderer rend = obj.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = mat;
            }
            return obj;
        }

        private static GameObject CreatePointLight(string name, Transform parent, Vector3 pos, Color color, float range, float intensity)
        {
            GameObject lightObj = new GameObject(name);
            if (parent != null) lightObj.transform.SetParent(parent, false);
            lightObj.transform.position = pos;
            Light lt = lightObj.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = color;
            lt.range = range;
            lt.intensity = intensity;
            lt.shadows = LightShadows.Soft;
            return lightObj;
        }

        private static GameObject CreateEnemyUnit(string name, Vector3 pos, Material bodyMat, Material eyeMat, Transform parent, int hp, int ac, int dmg)
        {
            GameObject enemyObj = new GameObject(name);
            if (parent != null) enemyObj.transform.SetParent(parent, false);
            enemyObj.transform.position = pos;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(enemyObj.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            if (bodyMat != null) body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            EnemyUnit enemy = enemyObj.AddComponent<EnemyUnit>();
            CapsuleCollider col = enemyObj.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.5f;
            col.center = new Vector3(0f, 0.9f, 0f);

            return enemyObj;
        }

        private static GameObject CreateBossUnit<T>(string name, Vector3 pos, Material bodyMat, Material accentMat, Transform parent) where T : EnemyUnit
        {
            GameObject bossObj = new GameObject(name);
            if (parent != null) bossObj.transform.SetParent(parent, false);
            bossObj.transform.position = pos;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "BossBody";
            body.transform.SetParent(bossObj.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            body.transform.localScale = new Vector3(1.3f, 1.4f, 1.3f);
            if (bodyMat != null) body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crown.name = "BossCrown";
            crown.transform.SetParent(bossObj.transform, false);
            crown.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            crown.transform.localScale = new Vector3(0.7f, 0.15f, 0.7f);
            if (accentMat != null) crown.GetComponent<Renderer>().sharedMaterial = accentMat;

            bossObj.AddComponent<T>();
            CapsuleCollider col = bossObj.AddComponent<CapsuleCollider>();
            col.height = 2.6f;
            col.radius = 0.8f;
            col.center = new Vector3(0f, 1.3f, 0f);

            return bossObj;
        }

        private static GameObject CreateBossDialogueNPC(string name, Vector3 pos, Material mat, string dialogueAssetPath, string prompt, Transform parent)
        {
            GameObject npcObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcObj.name = name;
            if (parent != null) npcObj.transform.SetParent(parent, false);
            npcObj.transform.position = pos;
            npcObj.transform.localScale = new Vector3(1.1f, 1.2f, 1.1f);

            if (mat != null)
            {
                Renderer rend = npcObj.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = mat;
            }

            VillageNPC vNPC = npcObj.AddComponent<VillageNPC>();
            vNPC.PromptMessage = prompt;
            vNPC.InteractionRadius = 4.5f;
            vNPC.IsInteractable = true;
            vNPC.StartingDialogueNode = AssetDatabase.LoadAssetAtPath<DialogueNodeSO>(dialogueAssetPath);

            return npcObj;
        }

        private static GameObject CreateRewardChest(string name, Vector3 pos, Material woodMat, Material goldMat, GameObject chestPrefab, Transform parent, int gold, ItemSO itemReward)
        {
            GameObject chestObj;
            if (chestPrefab != null)
            {
                chestObj = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab);
                if (parent != null) chestObj.transform.SetParent(parent, false);
                chestObj.name = name;
                chestObj.transform.position = pos;
            }
            else
            {
                chestObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chestObj.name = name;
                if (parent != null) chestObj.transform.SetParent(parent, false);
                chestObj.transform.position = pos;
                chestObj.transform.localScale = new Vector3(1.4f, 0.9f, 1.0f);
                if (woodMat != null) chestObj.GetComponent<Renderer>().sharedMaterial = woodMat;
            }

            ChestRewardInteraction reward = chestObj.GetComponent<ChestRewardInteraction>();
            if (reward == null) reward = chestObj.AddComponent<ChestRewardInteraction>();
            reward.GoldReward = gold;
            reward.ItemReward = itemReward;
            reward.EnsureChestCollider();

            return chestObj;
        }

        private static void CreateHerbPickup(string name, Vector3 pos, Material mat, ItemSO herbItem, Transform parent)
        {
            GameObject herb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            herb.name = name;
            if (parent != null) herb.transform.SetParent(parent, false);
            herb.transform.position = pos;
            herb.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            if (mat != null) herb.GetComponent<Renderer>().sharedMaterial = mat;

            ChestRewardInteraction reward = herb.AddComponent<ChestRewardInteraction>();
            reward.GoldReward = 0;
            reward.ItemReward = herbItem;
            reward.PromptMessage = "Gather Swamp Herb";
            reward.EnsureChestCollider();
        }

        private static void CreateCornerPillars(string prefix, Vector3 center, float halfX, float halfZ, float height, Material mat, GameObject prefab, Transform parent)
        {
            Vector3[] corners = new Vector3[]
            {
                new Vector3(center.x - halfX, height * 0.5f, center.z + halfZ),
                new Vector3(center.x + halfX, height * 0.5f, center.z + halfZ),
                new Vector3(center.x - halfX, height * 0.5f, center.z - halfZ),
                new Vector3(center.x + halfX, height * 0.5f, center.z - halfZ),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                if (prefab != null)
                {
                    GameObject p = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    p.name = $"{prefix}_{i}";
                    p.transform.position = new Vector3(corners[i].x, 0f, corners[i].z);
                    p.transform.localScale = new Vector3(1.2f, height / 5f, 1.2f);
                }
                else
                {
                    CreateCube($"{prefix}_{i}", corners[i], new Vector3(2.0f, height, 2.0f), mat, parent);
                }
            }
        }

        private static Material LoadMaterial(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void RegisterScenesInBuildSettings()
        {
            string[] scenesToRegister = new string[]
            {
                "Assets/Scenes/Zone_1_VillageAndCellar.unity",
                "Assets/Scenes/Zone_2_ForestPath.unity",
                "Assets/Scenes/Zone_3_CastleCourtyard.unity",
                "Assets/Scenes/Zone_4_Library.unity",
                "Assets/Scenes/Zone_5_CastleHall.unity",
                "Assets/Scenes/Zone_6_Tower.unity",
                "Assets/Scenes/Zone_7_ThroneRoom.unity"
            };

            EditorBuildSettingsScene[] buildSettings = new EditorBuildSettingsScene[scenesToRegister.Length];
            for (int i = 0; i < scenesToRegister.Length; i++)
            {
                GUID sceneGuid = new GUID(AssetDatabase.AssetPathToGUID(scenesToRegister[i]));
                buildSettings[i] = new EditorBuildSettingsScene(sceneGuid, true);
            }

            EditorBuildSettings.scenes = buildSettings;
            Debug.Log($"[ZoneSceneBuilder] Successfully registered {scenesToRegister.Length} scenes in EditorBuildSettings.scenes!");
        }

        #endregion
    }
}
