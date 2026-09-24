using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;
using CastleOfTheD20.Data;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Constructs enclosing walls, connecting doors/archways, and places bosses into their
    /// respective rooms matching the active scene planes: CourtYard, Forest, Library, ThroneRoom.
    /// </summary>
    public static class BuildDungeonWingsEditor
    {
        [MenuItem("CastleOfDice/Build Walls, Doors & Place Bosses", false, 1)]
        [MenuItem("Tools/Castle of Dice/Build Walls, Doors & Place Bosses", false, 1)]
        [MenuItem("CastleOfDice/Build Castle Dungeon Wings (3 Wings)", false, 11)]
        public static void BuildAllDungeonWings()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "StartVillage")
            {
                if (EditorUtility.DisplayDialog("Open StartVillage Scene?",
                    "StartVillage scene is required to build the castle dungeon wings. Open it now?", "Open", "Cancel"))
                {
                    activeScene = EditorSceneManager.OpenScene("Assets/Scenes/StartVillage.unity", OpenSceneMode.Single);
                }
                else
                {
                    return;
                }
            }

            // Ensure data assets exist
            GenerateGameDataEditor.GenerateAllGameAssets();

            Undo.SetCurrentGroupName("Build Walls, Doors & Place Bosses");
            int undoGroup = Undo.GetCurrentGroup();

            // Materials
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Rocks_1_2.mat");
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Logs.mat")
                ?? wallMat;
            Material metalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_HammerJAanvil.mat")
                ?? wallMat;
            Material goldMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Chest.mat")
                ?? wallMat;
            Material stoneFenceMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Stone_fence.mat")
                ?? wallMat;
            Material herbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Fern.mat")
                ?? woodMat;

            // Prefabs
            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Chest.prefab");
            GameObject combatGridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/CombatGrid.prefab");
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Arch.prefab");
            GameObject pineTreePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Pine_tree.prefab");
            GameObject stumpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Stump.prefab");
            GameObject rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Rock_1.prefab");
            GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Pillar.prefab");

            // Items for rewards
            ItemSO signetRing = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_SignetRing.asset");
            ItemSO greaterPotion = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_GreaterPotion.asset");
            ItemSO swampHerb = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_SwampHerb.asset");

            // Detect or fallback plane positions
            GameObject courtYardPlane = GameObject.Find("CourtYard");
            GameObject forestPlane = GameObject.Find("Forest");
            GameObject libraryPlane = GameObject.Find("Library");
            GameObject throneRoomPlane = GameObject.Find("ThroneRoom");

            Vector3 forestPos = forestPlane != null ? forestPlane.transform.position : new Vector3(0f, 0f, 109.2f);
            Vector3 courtyardPos = courtYardPlane != null ? courtYardPlane.transform.position : new Vector3(0f, 0f, 208.2f);
            Vector3 libraryPos = libraryPlane != null ? libraryPlane.transform.position : new Vector3(-99.9f, 0f, 208.2f);
            Vector3 throneRoomPos = throneRoomPlane != null ? throneRoomPlane.transform.position : new Vector3(0.3f, 0f, 307.2f);

            // Clean up existing Castle_Wings or Room_Structures container if present
            GameObject existingWings = GameObject.Find("Castle_Wings");
            if (existingWings != null)
            {
                Undo.DestroyObjectImmediate(existingWings);
            }
            GameObject existingStructures = GameObject.Find("Room_Structures");
            if (existingStructures != null)
            {
                Undo.DestroyObjectImmediate(existingStructures);
            }

            GameObject wingsRoot = new GameObject("Castle_Wings");
            Undo.RegisterCreatedObjectUndo(wingsRoot, "Create Castle Wings Root");

            // 1. Build Forest Zone & South Gate to Village
            BuildForestZone(wingsRoot.transform, forestPos, wallMat, woodMat, stoneFenceMat, herbMat, pineTreePrefab, stumpPrefab, rockPrefab, swampHerb);

            // 2. Build CourtYard & Cursed Commander Boss
            BuildCourtyardZone(wingsRoot.transform, courtyardPos, wallMat, woodMat, metalMat, goldMat, pillarPrefab, chestPrefab, combatGridPrefab, signetRing);

            // 3. Build Arcane Library & Shadow Mage Malakor Boss
            BuildLibraryZone(wingsRoot.transform, libraryPos, wallMat, woodMat, metalMat, goldMat, pillarPrefab, chestPrefab, combatGridPrefab, greaterPotion);

            // 4. Build Throne Room & The Gargoyle King Final Boss
            BuildThroneRoomZone(wingsRoot.transform, throneRoomPos, wallMat, woodMat, metalMat, goldMat, pillarPrefab, chestPrefab, combatGridPrefab);

            // 5. Build Connecting Doors, Gates and Portals between Rooms
            BuildConnectingDoorsAndGates(wingsRoot.transform, forestPos, courtyardPos, libraryPos, throneRoomPos, wallMat, archPrefab, pillarPrefab);

            // Connect Village Gate to Forest
            ConnectVillageGateToForest();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[BuildDungeonWingsEditor] All walls, doors, bosses and encounters successfully placed on CourtYard, Forest, Library, and ThroneRoom!");
        }

        #region Zone 1: Forest (Village to Castle Buffer & Swamp Herbs)

        private static void BuildForestZone(Transform root, Vector3 center, Material wallMat, Material woodMat, Material fenceMat, Material herbMat,
            GameObject treePrefab, GameObject stumpPrefab, GameObject rockPrefab, ItemSO swampHerbItem)
        {
            GameObject forestObj = new GameObject("Zone_Forest");
            forestObj.transform.SetParent(root, false);

            GameObject walls = new GameObject("Walls_And_Borders");
            walls.transform.SetParent(forestObj.transform, false);

            // West Natural Border Wall (X = -50)
            CreateCube("Forest_Border_West", new Vector3(center.x - 50f, 3f, center.z), new Vector3(1.5f, 6f, 100f), wallMat, walls.transform);

            // East Natural Border Wall (X = +50)
            CreateCube("Forest_Border_East", new Vector3(center.x + 50f, 3f, center.z), new Vector3(1.5f, 6f, 100f), wallMat, walls.transform);

            // South Wall Segments (with opening from -4 to +4 for Village gate)
            CreateCube("Forest_Wall_South_L", new Vector3(center.x - 27f, 3f, center.z - 50f), new Vector3(46f, 6f, 1.5f), wallMat, walls.transform);
            CreateCube("Forest_Wall_South_R", new Vector3(center.x + 27f, 3f, center.z - 50f), new Vector3(46f, 6f, 1.5f), wallMat, walls.transform);

            // North Wall Segments (with opening from -4 to +4 for Castle gate)
            CreateCube("Forest_Wall_North_L", new Vector3(center.x - 27f, 3f, center.z + 49.5f), new Vector3(46f, 6f, 1.5f), wallMat, walls.transform);
            CreateCube("Forest_Wall_North_R", new Vector3(center.x + 27f, 3f, center.z + 49.5f), new Vector3(46f, 6f, 1.5f), wallMat, walls.transform);

            // Atmospheric Lighting
            CreatePointLight("ForestLight_South", forestObj.transform, new Vector3(center.x, 6f, center.z - 25f), new Color(0.55f, 0.85f, 0.45f), 30f, 2.0f);
            CreatePointLight("ForestLight_North", forestObj.transform, new Vector3(center.x, 6f, center.z + 25f), new Color(0.45f, 0.75f, 0.65f), 30f, 2.0f);

            // Mirabel's Swamp Herb Interactive Pickups
            GameObject herbsContainer = new GameObject("Swamp_Herb_Pickups");
            herbsContainer.transform.SetParent(forestObj.transform, false);

            CreateHerbPickup("SwampHerb_1", new Vector3(center.x - 14f, 0.4f, center.z - 15f), herbMat, swampHerbItem, herbsContainer.transform);
            CreateHerbPickup("SwampHerb_2", new Vector3(center.x + 16f, 0.4f, center.z + 5f), herbMat, swampHerbItem, herbsContainer.transform);
            CreateHerbPickup("SwampHerb_3", new Vector3(center.x - 12f, 0.4f, center.z + 28f), herbMat, swampHerbItem, herbsContainer.transform);

            // Decor: Trees, Stumps & Rocks on flanks
            GameObject props = new GameObject("Forest_Flora_And_Rocks");
            props.transform.SetParent(forestObj.transform, false);

            Vector3[] treePositions = new Vector3[]
            {
                new Vector3(center.x - 25f, 0f, center.z - 30f),
                new Vector3(center.x - 35f, 0f, center.z - 10f),
                new Vector3(center.x - 20f, 0f, center.z + 15f),
                new Vector3(center.x - 30f, 0f, center.z + 35f),
                new Vector3(center.x + 25f, 0f, center.z - 35f),
                new Vector3(center.x + 35f, 0f, center.z - 5f),
                new Vector3(center.x + 22f, 0f, center.z + 20f),
                new Vector3(center.x + 32f, 0f, center.z + 40f),
            };

            foreach (var pos in treePositions)
            {
                if (treePrefab != null)
                {
                    GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, props.transform);
                    tree.transform.position = pos;
                }
                else
                {
                    CreateCube("Forest_Tree", pos + new Vector3(0f, 3f, 0f), new Vector3(1.2f, 6f, 1.2f), woodMat, props.transform);
                }
            }
        }

        #endregion

        #region Zone 2: CourtYard & Cursed Commander Boss

        private static void BuildCourtyardZone(Transform root, Vector3 center, Material wallMat, Material woodMat, Material metalMat, Material goldMat,
            GameObject pillarPrefab, GameObject chestPrefab, GameObject gridPrefab, ItemSO signetRing)
        {
            GameObject wingObj = new GameObject("Zone_CourtYard");
            wingObj.transform.SetParent(root, false);

            // Perimeter Walls
            GameObject walls = new GameObject("Walls");
            walls.transform.SetParent(wingObj.transform, false);

            // East Outer Wall (X = +50)
            CreateCube("CourtYard_Wall_East", new Vector3(center.x + 50f, 3.5f, center.z), new Vector3(1.5f, 7f, 100f), wallMat, walls.transform);

            // West Wall Segments (with opening from Z = 204.2 to 212.2 for Library archway)
            CreateCube("CourtYard_Wall_West_S", new Vector3(center.x - 50f, 3.5f, center.z - 27f), new Vector3(1.5f, 7f, 46f), wallMat, walls.transform);
            CreateCube("CourtYard_Wall_West_N", new Vector3(center.x - 50f, 3.5f, center.z + 27f), new Vector3(1.5f, 7f, 46f), wallMat, walls.transform);

            // North Wall Segments (with opening from -4 to +4 for Throne Room gate)
            CreateCube("CourtYard_Wall_North_L", new Vector3(center.x - 27f, 3.5f, center.z + 50f), new Vector3(46f, 7f, 1.5f), wallMat, walls.transform);
            CreateCube("CourtYard_Wall_North_R", new Vector3(center.x + 27f, 3.5f, center.z + 50f), new Vector3(46f, 7f, 1.5f), wallMat, walls.transform);

            // Corner Pillars
            CreateCornerPillars("Courtyard_Pillar", center, 47f, 47f, 7f, wallMat, pillarPrefab, walls.transform);

            // Courtyard Lighting
            CreatePointLight("CourtyardLight_Center", wingObj.transform, new Vector3(center.x, 5.5f, center.z), new Color(1f, 0.75f, 0.45f), 28f, 2.5f);
            CreatePointLight("CourtyardLight_North", wingObj.transform, new Vector3(center.x, 5.0f, center.z + 30f), new Color(1f, 0.65f, 0.35f), 22f, 2.0f);

            // Tactical Grid
            if (gridPrefab != null)
            {
                GameObject grid = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, wingObj.transform);
                grid.name = "CombatGrid_Courtyard";
                grid.transform.position = new Vector3(center.x, 0.05f, center.z + 10f);
            }

            // Enemies (2 Skeletons + Cursed Commander Boss)
            GameObject enemiesContainer = new GameObject("Enemies");
            enemiesContainer.transform.SetParent(wingObj.transform, false);

            List<GameObject> enemyList = new List<GameObject>();

            GameObject skel1 = CreateEnemyUnit("Courtyard_Skeleton_1", new Vector3(center.x - 6f, 0.9f, center.z + 12f), woodMat, metalMat, enemiesContainer.transform, 20, 12, 4);
            skel1.SetActive(false);
            enemyList.Add(skel1);

            GameObject skel2 = CreateEnemyUnit("Courtyard_Skeleton_2", new Vector3(center.x + 6f, 0.9f, center.z + 12f), woodMat, metalMat, enemiesContainer.transform, 20, 12, 4);
            skel2.SetActive(false);
            enemyList.Add(skel2);

            // Boss: Cursed Commander
            GameObject bossObj = CreateBossUnit<CursedCommanderBoss>("Boss_CursedCommander", new Vector3(center.x, 1.2f, center.z + 18f), metalMat, goldMat, enemiesContainer.transform);
            bossObj.SetActive(false);
            enemyList.Add(bossObj);

            // Pre-combat Dialogue NPC
            CreateBossDialogueNPC("NPC_CursedCommander", new Vector3(center.x, 1.0f, center.z - 15f), metalMat, "Assets/Data/Dialogues/Commander_Intro.asset", "Haasta Kirottu Komentaja", wingObj.transform);

            // Reward Chest (Contains Othelia's Signet Ring + 40 Gold)
            GameObject chestObj = CreateRewardChest("Courtyard_Reward_Chest", new Vector3(center.x, 0.4f, center.z + 30f), woodMat, goldMat, chestPrefab, wingObj.transform, 40, signetRing);
            chestObj.SetActive(false);

            // Encounter Controller
            GameObject triggerObj = new GameObject("Courtyard_Encounter_Trigger");
            triggerObj.transform.SetParent(wingObj.transform, false);
            triggerObj.transform.position = new Vector3(center.x, 2.5f, center.z + 10f);
            BoxCollider trigCol = triggerObj.AddComponent<BoxCollider>();
            trigCol.size = new Vector3(32f, 6f, 32f);
            trigCol.isTrigger = true;

            DungeonRoomController room = triggerObj.AddComponent<DungeonRoomController>();
            room.roomLocation = "Courtyard";
            room.bossIdentifier = "CursedCommander";
            room.roomEnemies = enemyList;
            room.secretPassageOrChest = chestObj;
        }

        #endregion

        #region Zone 3: Arcane Library & Shadow Mage Malakor Boss

        private static void BuildLibraryZone(Transform root, Vector3 center, Material wallMat, Material woodMat, Material metalMat, Material goldMat,
            GameObject pillarPrefab, GameObject chestPrefab, GameObject gridPrefab, ItemSO potionReward)
        {
            GameObject wingObj = new GameObject("Zone_Library");
            wingObj.transform.SetParent(root, false);

            // Perimeter Walls
            GameObject walls = new GameObject("Walls");
            walls.transform.SetParent(wingObj.transform, false);

            // West Outer Wall (X = -149.9)
            CreateCube("Library_Wall_West", new Vector3(center.x - 50f, 3.5f, center.z), new Vector3(1.5f, 7f, 100f), wallMat, walls.transform);

            // North Outer Wall (Z = 258.2)
            CreateCube("Library_Wall_North", new Vector3(center.x, 3.5f, center.z + 50f), new Vector3(100f, 7f, 1.5f), wallMat, walls.transform);

            // South Outer Wall (Z = 158.2)
            CreateCube("Library_Wall_South", new Vector3(center.x, 3.5f, center.z - 50f), new Vector3(100f, 7f, 1.5f), wallMat, walls.transform);

            // Corner Pillars
            CreateCornerPillars("Library_Pillar", center, 47f, 47f, 7f, wallMat, pillarPrefab, walls.transform);

            // Blue Arcane Mystical Lighting
            CreatePointLight("ArcaneLight_Center", wingObj.transform, new Vector3(center.x, 5.0f, center.z), new Color(0.25f, 0.55f, 1.0f), 28f, 2.5f);
            CreatePointLight("ArcaneLight_East", wingObj.transform, new Vector3(center.x + 20f, 4.0f, center.z + 15f), new Color(0.45f, 0.25f, 0.95f), 20f, 2.0f);
            CreatePointLight("ArcaneLight_West", wingObj.transform, new Vector3(center.x - 20f, 4.0f, center.z + 15f), new Color(0.45f, 0.25f, 0.95f), 20f, 2.0f);

            // Tactical Grid
            if (gridPrefab != null)
            {
                GameObject grid = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, wingObj.transform);
                grid.name = "CombatGrid_Library";
                grid.transform.position = new Vector3(center.x, 0.05f, center.z + 10f);
            }

            // Enemies (Boss: Shadow Mage Malakor)
            GameObject enemiesContainer = new GameObject("Enemies");
            enemiesContainer.transform.SetParent(wingObj.transform, false);

            List<GameObject> enemyList = new List<GameObject>();
            GameObject bossObj = CreateBossUnit<ShadowMageMalakorBoss>("Boss_ShadowMageMalakor", new Vector3(center.x, 1.1f, center.z + 18f), wallMat, metalMat, enemiesContainer.transform);
            bossObj.SetActive(false);
            enemyList.Add(bossObj);

            // Pre-combat Dialogue NPC
            CreateBossDialogueNPC("NPC_Malakor", new Vector3(center.x, 1.0f, center.z - 15f), metalMat, "Assets/Data/Dialogues/Malakor_Intro.asset", "Haasta Varjomaagi Malakor", wingObj.transform);

            // Reward Chest (Greater potion + 60 gold)
            GameObject chestObj = CreateRewardChest("Library_Reward_Chest", new Vector3(center.x, 0.4f, center.z + 30f), woodMat, goldMat, chestPrefab, wingObj.transform, 60, potionReward);
            chestObj.SetActive(false);

            // Encounter Controller
            GameObject triggerObj = new GameObject("Library_Encounter_Trigger");
            triggerObj.transform.SetParent(wingObj.transform, false);
            triggerObj.transform.position = new Vector3(center.x, 2.5f, center.z + 10f);
            BoxCollider trigCol = triggerObj.AddComponent<BoxCollider>();
            trigCol.size = new Vector3(32f, 6f, 32f);
            trigCol.isTrigger = true;

            DungeonRoomController room = triggerObj.AddComponent<DungeonRoomController>();
            room.roomLocation = "Library";
            room.bossIdentifier = "ShadowMageMalakor";
            room.roomEnemies = enemyList;
            room.secretPassageOrChest = chestObj;
        }

        #endregion

        #region Zone 4: Throne Room & The Gargoyle King Final Boss

        private static void BuildThroneRoomZone(Transform root, Vector3 center, Material wallMat, Material woodMat, Material metalMat, Material goldMat,
            GameObject pillarPrefab, GameObject chestPrefab, GameObject gridPrefab)
        {
            GameObject wingObj = new GameObject("Zone_ThroneRoom");
            wingObj.transform.SetParent(root, false);

            // Perimeter Walls
            GameObject walls = new GameObject("Walls");
            walls.transform.SetParent(wingObj.transform, false);

            // North Monumental Wall (Z = 357.2)
            CreateCube("ThroneRoom_Wall_North", new Vector3(center.x, 4.0f, center.z + 50f), new Vector3(100f, 8f, 1.5f), wallMat, walls.transform);

            // West Outer Wall (X = -49.7)
            CreateCube("ThroneRoom_Wall_West", new Vector3(center.x - 50f, 4.0f, center.z), new Vector3(1.5f, 8f, 100f), wallMat, walls.transform);

            // East Outer Wall (X = +50.3)
            CreateCube("ThroneRoom_Wall_East", new Vector3(center.x + 50f, 4.0f, center.z), new Vector3(1.5f, 8f, 100f), wallMat, walls.transform);

            // Throne Dais Platform
            GameObject dais = CreateCube("Throne_Dais", new Vector3(center.x, 0.6f, center.z + 32f), new Vector3(14f, 1.2f, 8f), wallMat, wingObj.transform);

            // Imperial Support Columns
            CreateCube("Throne_Col_L", new Vector3(center.x - 6f, 4.0f, center.z + 32f), new Vector3(1.8f, 8f, 1.8f), wallMat, wingObj.transform);
            CreateCube("Throne_Col_R", new Vector3(center.x + 6f, 4.0f, center.z + 32f), new Vector3(1.8f, 8f, 1.8f), wallMat, wingObj.transform);
            CreateCornerPillars("ThroneRoom_Pillar", center, 47f, 47f, 8f, wallMat, pillarPrefab, walls.transform);

            // Majestic Crimson & Gold Lighting
            CreatePointLight("CrownLight_Center", wingObj.transform, new Vector3(center.x, 6.5f, center.z + 10f), new Color(1f, 0.40f, 0.15f), 32f, 3.0f);
            CreatePointLight("CrownLight_Throne", wingObj.transform, new Vector3(center.x, 5.0f, center.z + 32f), new Color(1f, 0.85f, 0.25f), 24f, 2.5f);

            // Tactical Grid
            if (gridPrefab != null)
            {
                GameObject grid = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, wingObj.transform);
                grid.name = "CombatGrid_CrownHall";
                grid.transform.position = new Vector3(center.x, 0.05f, center.z + 12f);
            }

            // Boss: The Gargoyle King
            GameObject enemiesContainer = new GameObject("Enemies");
            enemiesContainer.transform.SetParent(wingObj.transform, false);

            List<GameObject> enemyList = new List<GameObject>();
            GameObject bossObj = CreateBossUnit<GargoyleKingBoss>("Boss_GargoyleKing", new Vector3(center.x, 1.8f, center.z + 28f), wallMat, goldMat, enemiesContainer.transform);
            bossObj.SetActive(false);
            enemyList.Add(bossObj);

            // Pre-combat Dialogue NPC
            CreateBossDialogueNPC("NPC_GargoyleKing", new Vector3(center.x, 1.2f, center.z - 10f), wallMat, "Assets/Data/Dialogues/GargoyleKing_Intro.asset", "Haasta Kivettymiskuningas", wingObj.transform);

            // Royal Treasure Chest (100 Gold + Campaign Victory)
            GameObject chestObj = CreateRewardChest("CrownHall_Treasure_Chest", new Vector3(center.x, 1.4f, center.z + 34f), woodMat, goldMat, chestPrefab, wingObj.transform, 100, null);
            chestObj.SetActive(false);

            // Encounter Controller
            GameObject triggerObj = new GameObject("CrownHall_Encounter_Trigger");
            triggerObj.transform.SetParent(wingObj.transform, false);
            triggerObj.transform.position = new Vector3(center.x, 2.5f, center.z + 15f);
            BoxCollider trigCol = triggerObj.AddComponent<BoxCollider>();
            trigCol.size = new Vector3(36f, 6f, 36f);
            trigCol.isTrigger = true;

            DungeonRoomController room = triggerObj.AddComponent<DungeonRoomController>();
            room.roomLocation = "CrownHall";
            room.bossIdentifier = "GargoyleKing";
            room.roomEnemies = enemyList;
            room.secretPassageOrChest = chestObj;
        }

        #endregion

        #region Doors & Archway Portals

        private static void BuildConnectingDoorsAndGates(Transform root, Vector3 forestPos, Vector3 courtyardPos, Vector3 libraryPos, Vector3 throneRoomPos,
            Material wallMat, GameObject archPrefab, GameObject pillarPrefab)
        {
            GameObject doorsRoot = new GameObject("Doors_And_Passages");
            doorsRoot.transform.SetParent(root, false);

            // 1. Door: Village <-> Forest
            // Boundary at Z ≈ 59.2, X ≈ 0
            Vector3 door1Pos = new Vector3(0f, 0f, 59.2f);
            CreateDoorway("Door_Village_Forest", door1Pos, Quaternion.identity,
                "Astu Syvään Metsään (Forest)", "Forest", new Vector3(0f, 0.5f, 65f),
                "Palaa Kivenkolon kylään (Village)", "Village", new Vector3(0f, 0.5f, 53f),
                wallMat, archPrefab, pillarPrefab, doorsRoot.transform);

            // 2. Door: Forest <-> CourtYard (Castle Outer Gate)
            // Boundary at Z ≈ 158.5, X ≈ 0
            Vector3 door2Pos = new Vector3(0f, 0f, 158.5f);
            CreateDoorway("Door_Forest_Courtyard", door2Pos, Quaternion.identity,
                "Astu Linnan Alapihalle (Wing 1: Courtyard)", "Courtyard", new Vector3(0f, 0.5f, 166f),
                "Palaa Metsään (Forest)", "Forest", new Vector3(0f, 0.5f, 150f),
                wallMat, archPrefab, pillarPrefab, doorsRoot.transform);

            // 3. Door: CourtYard <-> Library (Arcane Archway)
            // Boundary at X ≈ -50.0, Z ≈ 208.2 (rotated 90 degrees around Y)
            Vector3 door3Pos = new Vector3(-50.0f, 0f, 208.2f);
            CreateDoorway("Door_Courtyard_Library", door3Pos, Quaternion.Euler(0f, 90f, 0f),
                "Astu Salatieteen Kirjastoon (Wing 2: Library)", "Library", new Vector3(-58f, 0.5f, 208.2f),
                "Palaa Alapihalle (Wing 1: Courtyard)", "Courtyard", new Vector3(-42f, 0.5f, 208.2f),
                wallMat, archPrefab, pillarPrefab, doorsRoot.transform);

            // 4. Door: CourtYard <-> ThroneRoom (Crown Hall Iron Gate)
            // Boundary at Z ≈ 257.7, X ≈ 0.15
            Vector3 door4Pos = new Vector3(0.15f, 0f, 257.7f);
            CreateDoorway("Door_Courtyard_ThroneRoom", door4Pos, Quaternion.identity,
                "Astu Kruununsaliin (Wing 3: Final Boss)", "CrownHall", new Vector3(0.3f, 0.5f, 266f),
                "Palaa Alapihalle (Wing 1: Courtyard)", "Courtyard", new Vector3(0f, 0.5f, 250f),
                wallMat, archPrefab, pillarPrefab, doorsRoot.transform);
        }

        private static GameObject CreateDoorway(string name, Vector3 pos, Quaternion rot,
            string promptForward, string zoneForward, Vector3 spawnForward,
            string promptBackward, string zoneBackward, Vector3 spawnBackward,
            Material wallMat, GameObject archPrefab, GameObject pillarPrefab, Transform parent)
        {
            GameObject doorObj = new GameObject(name);
            doorObj.transform.SetParent(parent, false);
            doorObj.transform.position = pos;
            doorObj.transform.rotation = rot;

            // Visual Stone Arch
            if (archPrefab != null)
            {
                GameObject arch = (GameObject)PrefabUtility.InstantiatePrefab(archPrefab, doorObj.transform);
                arch.name = "Arch_Model";
                arch.transform.localPosition = Vector3.zero;
                arch.transform.localRotation = Quaternion.identity;
                arch.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
            }
            else
            {
                // Fallback Archway Pillars & Header
                CreateCube("Arch_Pillar_L", pos + rot * new Vector3(-3.5f, 2.5f, 0f), new Vector3(1.4f, 5f, 1.4f), wallMat, doorObj.transform);
                CreateCube("Arch_Pillar_R", pos + rot * new Vector3(3.5f, 2.5f, 0f), new Vector3(1.4f, 5f, 1.4f), wallMat, doorObj.transform);
                CreateCube("Arch_Header", pos + rot * new Vector3(0f, 5.2f, 0f), new Vector3(8.4f, 1.0f, 1.5f), wallMat, doorObj.transform);
            }

            // Flanking Decorative Pillars
            if (pillarPrefab != null)
            {
                GameObject pL = (GameObject)PrefabUtility.InstantiatePrefab(pillarPrefab, doorObj.transform);
                pL.transform.localPosition = new Vector3(-4.5f, 0f, 0f);
                GameObject pR = (GameObject)PrefabUtility.InstantiatePrefab(pillarPrefab, doorObj.transform);
                pR.transform.localPosition = new Vector3(4.5f, 0f, 0f);
            }

            // Warm Torch Lights
            CreatePointLight("DoorLight_Front", doorObj.transform, pos + rot * new Vector3(0f, 3.5f, -1.0f), new Color(1f, 0.75f, 0.4f), 12f, 1.5f);
            CreatePointLight("DoorLight_Back", doorObj.transform, pos + rot * new Vector3(0f, 3.5f, 1.0f), new Color(1f, 0.75f, 0.4f), 12f, 1.5f);

            // Forward Teleporter Trigger
            GameObject fwdTrigger = new GameObject(name + "_ForwardPortal");
            fwdTrigger.transform.SetParent(doorObj.transform, false);
            fwdTrigger.transform.localPosition = new Vector3(0f, 1.5f, -0.6f);
            BoxCollider fwdCol = fwdTrigger.AddComponent<BoxCollider>();
            fwdCol.size = new Vector3(5.5f, 3.5f, 1.6f);
            fwdCol.isTrigger = true;

            DoorTeleporter dtForward = fwdTrigger.AddComponent<DoorTeleporter>();
            dtForward.PromptMessage = promptForward;
            dtForward.DestinationZone = zoneForward;
            dtForward.TargetSpawnPosition = spawnForward;
            dtForward.InteractionRadius = 4.5f;
            dtForward.TriggerOnWalk = false;

            // Backward Teleporter Trigger
            GameObject bwdTrigger = new GameObject(name + "_BackwardPortal");
            bwdTrigger.transform.SetParent(doorObj.transform, false);
            bwdTrigger.transform.localPosition = new Vector3(0f, 1.5f, 0.6f);
            BoxCollider bwdCol = bwdTrigger.AddComponent<BoxCollider>();
            bwdCol.size = new Vector3(5.5f, 3.5f, 1.6f);
            bwdCol.isTrigger = true;

            DoorTeleporter dtBackward = bwdTrigger.AddComponent<DoorTeleporter>();
            dtBackward.PromptMessage = promptBackward;
            dtBackward.DestinationZone = zoneBackward;
            dtBackward.TargetSpawnPosition = spawnBackward;
            dtBackward.InteractionRadius = 4.5f;
            dtBackward.TriggerOnWalk = false;

            return doorObj;
        }

        private static void ConnectVillageGateToForest()
        {
            GameObject portal = GameObject.Find("Castle_Gate_Portal");
            if (portal != null)
            {
                DoorTeleporter dt = portal.GetComponent<DoorTeleporter>();
                if (dt != null)
                {
                    dt.PromptMessage = "Astu Syvään Metsään (Forest)";
                    dt.DestinationZone = "Forest";
                    dt.TargetSpawnPosition = new Vector3(0f, 0.5f, 65.0f);
                }
            }
        }

        #endregion

        #region Helpers

        private static GameObject CreateCube(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.position = pos;
            obj.transform.localScale = scale;
            if (mat != null)
            {
                Renderer rend = obj.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = mat;
            }
            return obj;
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

        private static GameObject CreatePointLight(string name, Transform parent, Vector3 pos, Color color, float range, float intensity)
        {
            GameObject lightObj = new GameObject(name);
            lightObj.transform.SetParent(parent, false);
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
            enemyObj.transform.SetParent(parent, false);
            enemyObj.transform.position = pos;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(enemyObj.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            if (bodyMat != null) body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            enemyObj.AddComponent<EnemyUnit>();
            CapsuleCollider col = enemyObj.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.5f;
            col.center = new Vector3(0f, 0.9f, 0f);

            return enemyObj;
        }

        private static GameObject CreateBossUnit<T>(string name, Vector3 pos, Material bodyMat, Material accentMat, Transform parent) where T : EnemyUnit
        {
            GameObject bossObj = new GameObject(name);
            bossObj.transform.SetParent(parent, false);
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
            npcObj.transform.SetParent(parent, false);
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
                chestObj = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, parent);
                chestObj.name = name;
                chestObj.transform.position = pos;
            }
            else
            {
                chestObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chestObj.name = name;
                chestObj.transform.SetParent(parent, false);
                chestObj.transform.position = pos;
                chestObj.transform.localScale = new Vector3(1.4f, 0.9f, 1.0f);
                if (woodMat != null) chestObj.GetComponent<Renderer>().sharedMaterial = woodMat;
            }

            ChestRewardInteraction reward = chestObj.GetComponent<ChestRewardInteraction>();
            if (reward == null)
            {
                reward = chestObj.AddComponent<ChestRewardInteraction>();
            }
            reward.GoldReward = gold;
            if (itemReward != null)
            {
                reward.ItemReward = itemReward;
            }
            reward.EnsureChestCollider();

            return chestObj;
        }

        private static GameObject CreateHerbPickup(string name, Vector3 pos, Material herbMat, ItemSO herbItem, Transform parent)
        {
            GameObject herbObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            herbObj.name = name;
            herbObj.transform.SetParent(parent, false);
            herbObj.transform.position = pos;
            herbObj.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

            if (herbMat != null)
            {
                Renderer rend = herbObj.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = herbMat;
            }

            ChestRewardInteraction reward = herbObj.AddComponent<ChestRewardInteraction>();
            reward.GoldReward = 0;
            if (herbItem != null)
            {
                reward.ItemReward = herbItem;
            }
            reward.PromptMessage = "Kerää Suokukka (Mirabelin tehtävä)";
            reward.InteractionRadius = 4.0f;
            reward.EnsureChestCollider();

            return herbObj;
        }

        #endregion
    }
}
