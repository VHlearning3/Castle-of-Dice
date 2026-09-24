using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Editor automation tool that constructs the 3 gothic castle wings:
    /// Wing 1: Courtyard & Watchtower (Cursed Commander)
    /// Wing 2: Arcane Library (Shadow Mage Malakor)
    /// Wing 3: Crown Hall (The Gargoyle King)
    /// Complete with tactical combat grids, pre-combat dialogue triggers, reward chests, and inter-wing portals.
    /// </summary>
    public static class BuildDungeonWingsEditor
    {
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

            Undo.SetCurrentGroupName("Build Castle Dungeon Wings");
            int undoGroup = Undo.GetCurrentGroup();

            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Rocks_1_2.mat");
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Logs.mat")
                ?? wallMat;
            Material metalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_HammerJAanvil.mat")
                ?? wallMat;
            Material goldMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Chest.mat")
                ?? wallMat;

            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Chest.prefab");
            GameObject combatGridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/CombatGrid.prefab");

            // Clean up existing Castle_Wings container if present
            GameObject existingWings = GameObject.Find("Castle_Wings");
            if (existingWings != null)
            {
                Undo.DestroyObjectImmediate(existingWings);
            }

            GameObject wingsRoot = new GameObject("Castle_Wings");
            Undo.RegisterCreatedObjectUndo(wingsRoot, "Create Castle Wings Root");

            // Connect village northern gate teleporter to Wing 1
            ConnectVillageGateToWing1();

            // 1. Build Wing 1: Courtyard (y = -40)
            BuildWing1Courtyard(wingsRoot.transform, wallMat, woodMat, metalMat, goldMat, chestPrefab, combatGridPrefab);

            // 2. Build Wing 2: Arcane Library (y = -70)
            BuildWing2Library(wingsRoot.transform, wallMat, woodMat, metalMat, goldMat, chestPrefab, combatGridPrefab);

            // 3. Build Wing 3: Crown Hall (y = -100)
            BuildWing3CrownHall(wingsRoot.transform, wallMat, woodMat, metalMat, goldMat, chestPrefab, combatGridPrefab);

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[BuildDungeonWingsEditor] All 3 Castle Dungeon Wings successfully constructed and wired in StartVillage.unity!");
        }

        private static void ConnectVillageGateToWing1()
        {
            GameObject portal = GameObject.Find("Castle_Gate_Portal");
            if (portal != null)
            {
                DoorTeleporter dt = portal.GetComponent<DoorTeleporter>();
                if (dt != null)
                {
                    dt.DestinationZone = "Courtyard";
                    dt.TargetSpawnPosition = new Vector3(0f, -39.8f, -7.0f);
                }
            }
        }

        #region Wing 1: Courtyard & Watchtower (Cursed Commander)

        private static void BuildWing1Courtyard(Transform root, Material wallMat, Material woodMat, Material metalMat, Material goldMat, GameObject chestPrefab, GameObject gridPrefab)
        {
            Vector3 origin = new Vector3(0f, -40f, 0f);
            GameObject wing = new GameObject("Wing1_Courtyard");
            wing.transform.SetParent(root, false);
            wing.transform.position = origin;

            // Geometry
            GameObject geo = new GameObject("Geometry");
            geo.transform.SetParent(wing.transform, false);
            CreateCube("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(22f, 1f, 22f), wallMat, geo.transform);
            CreateCube("Wall_N", new Vector3(0f, 3.5f, 11f), new Vector3(22f, 7f, 1f), wallMat, geo.transform);
            CreateCube("Wall_S", new Vector3(0f, 3.5f, -11f), new Vector3(22f, 7f, 1f), wallMat, geo.transform);
            CreateCube("Wall_E", new Vector3(11f, 3.5f, 0f), new Vector3(1f, 7f, 22f), wallMat, geo.transform);
            CreateCube("Wall_W", new Vector3(-11f, 3.5f, 0f), new Vector3(1f, 7f, 22f), wallMat, geo.transform);

            // Corner pillars
            CreateCube("Pillar_NW", new Vector3(-6f, 3.5f, 6f), new Vector3(1.5f, 7f, 1.5f), wallMat, geo.transform);
            CreateCube("Pillar_NE", new Vector3(6f, 3.5f, 6f), new Vector3(1.5f, 7f, 1.5f), wallMat, geo.transform);
            CreateCube("Pillar_SW", new Vector3(-6f, 3.5f, -6f), new Vector3(1.5f, 7f, 1.5f), wallMat, geo.transform);
            CreateCube("Pillar_SE", new Vector3(6f, 3.5f, -6f), new Vector3(1.5f, 7f, 1.5f), wallMat, geo.transform);

            // Lighting (Warm courtyard flames)
            CreatePointLight("CourtyardLight", wing.transform, new Vector3(0f, 5.0f, 0f), new Color(1f, 0.7f, 0.4f), 25f, 2.5f);

            // Player Spawn & Exit to Village
            GameObject spawnPoint = new GameObject("Courtyard_PlayerSpawnPoint");
            spawnPoint.transform.SetParent(wing.transform, false);
            spawnPoint.transform.localPosition = new Vector3(0f, 0.2f, -7.0f);

            GameObject returnPortal = new GameObject("Courtyard_ReturnPortal");
            returnPortal.transform.SetParent(wing.transform, false);
            returnPortal.transform.localPosition = new Vector3(0f, 0.5f, -9.5f);
            BoxCollider retCol = returnPortal.AddComponent<BoxCollider>();
            retCol.size = new Vector3(3f, 3f, 1.5f);
            retCol.isTrigger = true;
            DoorTeleporter retTeleport = returnPortal.AddComponent<DoorTeleporter>();
            retTeleport.PromptMessage = "Palaa Kivenkolon kylään";
            retTeleport.DestinationZone = "Village";
            retTeleport.TargetSpawnPosition = new Vector3(0f, 0.5f, 29.0f);

            // Tactical Grid
            if (gridPrefab != null)
            {
                GameObject grid = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, wing.transform);
                grid.name = "CombatGrid_Courtyard";
                grid.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            }

            // Enemies (2 Skeletons + Cursed Commander Boss)
            GameObject enemiesContainer = new GameObject("Enemies");
            enemiesContainer.transform.SetParent(wing.transform, false);

            List<GameObject> enemyList = new List<GameObject>();

            // Skeleton 1 & 2
            GameObject skel1 = CreateEnemyUnit("Courtyard_Skeleton_1", new Vector3(-3.5f, 0.9f, 3.0f), woodMat, metalMat, enemiesContainer.transform, 20, 12, 4);
            skel1.SetActive(false);
            enemyList.Add(skel1);

            GameObject skel2 = CreateEnemyUnit("Courtyard_Skeleton_2", new Vector3(3.5f, 0.9f, 3.0f), woodMat, metalMat, enemiesContainer.transform, 20, 12, 4);
            skel2.SetActive(false);
            enemyList.Add(skel2);

            // Boss: Cursed Commander
            GameObject bossObj = CreateBossUnit<CursedCommanderBoss>("Boss_CursedCommander", new Vector3(0f, 1.1f, 5.5f), metalMat, goldMat, enemiesContainer.transform);
            bossObj.SetActive(false);
            enemyList.Add(bossObj);

            // Pre-combat Dialogue NPC (Stands at boss location to trigger conversation before battle)
            GameObject bossNPC = CreateBossDialogueNPC("NPC_CursedCommander", new Vector3(0f, 1.0f, 2.0f), metalMat, "Assets/Data/Dialogues/Commander_Intro.asset", "Haasta Kirottu Komentaja", wing.transform);

            // Reward Chest (Contains Othelia's Signet Ring + 40 Gold)
            GameObject chestObj = CreateRewardChest("Courtyard_Reward_Chest", new Vector3(0f, 0.4f, 8.5f), woodMat, goldMat, chestPrefab, wing.transform, 40);
            chestObj.SetActive(false);

            // Portal to Wing 2 (Library)
            GameObject portalToWing2 = new GameObject("Portal_To_Library");
            portalToWing2.transform.SetParent(wing.transform, false);
            portalToWing2.transform.localPosition = new Vector3(0f, 0.5f, 9.5f);
            BoxCollider p2Col = portalToWing2.AddComponent<BoxCollider>();
            p2Col.size = new Vector3(3f, 3f, 1.5f);
            p2Col.isTrigger = true;
            DoorTeleporter p2Teleport = portalToWing2.AddComponent<DoorTeleporter>();
            p2Teleport.PromptMessage = "Astu Salatieteen Kirjastoon (Wing 2)";
            p2Teleport.DestinationZone = "Library";
            p2Teleport.TargetSpawnPosition = new Vector3(0f, -69.8f, -6.5f);
            portalToWing2.SetActive(false); // Unlocks when boss is defeated

            // Room Controller
            GameObject triggerObj = new GameObject("Courtyard_Encounter_Trigger");
            triggerObj.transform.SetParent(wing.transform, false);
            triggerObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            BoxCollider trigCol = triggerObj.AddComponent<BoxCollider>();
            trigCol.size = new Vector3(18f, 5f, 18f);
            trigCol.isTrigger = true;

            DungeonRoomController room = triggerObj.AddComponent<DungeonRoomController>();
            room.roomLocation = "Courtyard";
            room.bossIdentifier = "CursedCommander";
            room.roomEnemies = enemyList;
            room.secretPassageOrChest = chestObj;
            room.exitBarriers = new List<GameObject> { portalToWing2 };
        }

        #endregion

        #region Wing 2: Arcane Library (Shadow Mage Malakor)

        private static void BuildWing2Library(Transform root, Material wallMat, Material woodMat, Material metalMat, Material goldMat, GameObject chestPrefab, GameObject gridPrefab)
        {
            Vector3 origin = new Vector3(0f, -70f, 0f);
            GameObject wing = new GameObject("Wing2_Library");
            wing.transform.SetParent(root, false);
            wing.transform.position = origin;

            // Geometry
            GameObject geo = new GameObject("Geometry");
            geo.transform.SetParent(wing.transform, false);
            CreateCube("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f), wallMat, geo.transform);
            CreateCube("Wall_N", new Vector3(0f, 3.5f, 10f), new Vector3(20f, 7f, 1f), wallMat, geo.transform);
            CreateCube("Wall_S", new Vector3(0f, 3.5f, -10f), new Vector3(20f, 7f, 1f), wallMat, geo.transform);
            CreateCube("Wall_E", new Vector3(10f, 3.5f, 0f), new Vector3(1f, 7f, 20f), wallMat, geo.transform);
            CreateCube("Wall_W", new Vector3(-10f, 3.5f, 0f), new Vector3(1f, 7f, 20f), wallMat, geo.transform);

            // Blue Arcane Lighting
            CreatePointLight("ArcaneLight_Center", wing.transform, new Vector3(0f, 4.5f, 0f), new Color(0.2f, 0.5f, 1.0f), 22f, 2.2f);
            CreatePointLight("ArcaneLight_East", wing.transform, new Vector3(6f, 3.0f, 4f), new Color(0.4f, 0.2f, 0.9f), 15f, 1.5f);
            CreatePointLight("ArcaneLight_West", wing.transform, new Vector3(-6f, 3.0f, 4f), new Color(0.4f, 0.2f, 0.9f), 15f, 1.5f);

            // Player Spawn & Return to Courtyard
            GameObject spawnPoint = new GameObject("Library_PlayerSpawnPoint");
            spawnPoint.transform.SetParent(wing.transform, false);
            spawnPoint.transform.localPosition = new Vector3(0f, 0.2f, -6.5f);

            GameObject returnPortal = new GameObject("Library_ReturnPortal");
            returnPortal.transform.SetParent(wing.transform, false);
            returnPortal.transform.localPosition = new Vector3(0f, 0.5f, -8.5f);
            BoxCollider retCol = returnPortal.AddComponent<BoxCollider>();
            retCol.size = new Vector3(3f, 3f, 1.5f);
            retCol.isTrigger = true;
            DoorTeleporter retTeleport = returnPortal.AddComponent<DoorTeleporter>();
            retTeleport.PromptMessage = "Palaa Alapihalle (Wing 1)";
            retTeleport.DestinationZone = "Courtyard";
            retTeleport.TargetSpawnPosition = new Vector3(0f, -39.8f, 7.5f);

            // Tactical Grid
            if (gridPrefab != null)
            {
                GameObject grid = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, wing.transform);
                grid.name = "CombatGrid_Library";
                grid.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            }

            // Enemies (Boss: Shadow Mage Malakor)
            GameObject enemiesContainer = new GameObject("Enemies");
            enemiesContainer.transform.SetParent(wing.transform, false);

            List<GameObject> enemyList = new List<GameObject>();
            GameObject bossObj = CreateBossUnit<ShadowMageMalakorBoss>("Boss_ShadowMageMalakor", new Vector3(0f, 1.0f, 5.0f), wallMat, metalMat, enemiesContainer.transform);
            bossObj.SetActive(false);
            enemyList.Add(bossObj);

            // Pre-combat Dialogue NPC
            CreateBossDialogueNPC("NPC_Malakor", new Vector3(0f, 1.0f, 1.5f), metalMat, "Assets/Data/Dialogues/Malakor_Intro.asset", "Haasta Varjomaagi Malakor", wing.transform);

            // Reward Chest (Greater potion + 60 gold)
            GameObject chestObj = CreateRewardChest("Library_Reward_Chest", new Vector3(0f, 0.4f, 7.5f), woodMat, goldMat, chestPrefab, wing.transform, 60);
            chestObj.SetActive(false);

            // Portal to Wing 3 (Crown Hall)
            GameObject portalToWing3 = new GameObject("Portal_To_CrownHall");
            portalToWing3.transform.SetParent(wing.transform, false);
            portalToWing3.transform.localPosition = new Vector3(0f, 0.5f, 8.5f);
            BoxCollider p3Col = portalToWing3.AddComponent<BoxCollider>();
            p3Col.size = new Vector3(3f, 3f, 1.5f);
            p3Col.isTrigger = true;
            DoorTeleporter p3Teleport = portalToWing3.AddComponent<DoorTeleporter>();
            p3Teleport.PromptMessage = "Astu Kruununsaliin (Wing 3: Final Boss)";
            p3Teleport.DestinationZone = "CrownHall";
            p3Teleport.TargetSpawnPosition = new Vector3(0f, -99.8f, -7.5f);
            portalToWing3.SetActive(false);

            // Room Controller
            GameObject triggerObj = new GameObject("Library_Encounter_Trigger");
            triggerObj.transform.SetParent(wing.transform, false);
            triggerObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            BoxCollider trigCol = triggerObj.AddComponent<BoxCollider>();
            trigCol.size = new Vector3(16f, 5f, 16f);
            trigCol.isTrigger = true;

            DungeonRoomController room = triggerObj.AddComponent<DungeonRoomController>();
            room.roomLocation = "Library";
            room.bossIdentifier = "ShadowMageMalakor";
            room.roomEnemies = enemyList;
            room.secretPassageOrChest = chestObj;
            room.exitBarriers = new List<GameObject> { portalToWing3 };
        }

        #endregion

        #region Wing 3: Crown Hall (The Gargoyle King Final Boss)

        private static void BuildWing3CrownHall(Transform root, Material wallMat, Material woodMat, Material metalMat, Material goldMat, GameObject chestPrefab, GameObject gridPrefab)
        {
            Vector3 origin = new Vector3(0f, -100f, 0f);
            GameObject wing = new GameObject("Wing3_CrownHall");
            wing.transform.SetParent(root, false);
            wing.transform.position = origin;

            // Monumental Throne Hall Geometry
            GameObject geo = new GameObject("Geometry");
            geo.transform.SetParent(wing.transform, false);
            CreateCube("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(26f, 1f, 26f), wallMat, geo.transform);
            CreateCube("Wall_N", new Vector3(0f, 4.5f, 13f), new Vector3(26f, 9f, 1f), wallMat, geo.transform);
            CreateCube("Wall_S", new Vector3(0f, 4.5f, -13f), new Vector3(26f, 9f, 1f), wallMat, geo.transform);
            CreateCube("Wall_E", new Vector3(13f, 4.5f, 0f), new Vector3(1f, 9f, 26f), wallMat, geo.transform);
            CreateCube("Wall_W", new Vector3(-13f, 4.5f, 0f), new Vector3(1f, 9f, 26f), wallMat, geo.transform);

            // Grand Support Columns
            CreateCube("Col_NW", new Vector3(-7f, 4.5f, 7f), new Vector3(2.0f, 9f, 2.0f), wallMat, geo.transform);
            CreateCube("Col_NE", new Vector3(7f, 4.5f, 7f), new Vector3(2.0f, 9f, 2.0f), wallMat, geo.transform);
            CreateCube("Col_SW", new Vector3(-7f, 4.5f, -7f), new Vector3(2.0f, 9f, 2.0f), wallMat, geo.transform);
            CreateCube("Col_SE", new Vector3(7f, 4.5f, -7f), new Vector3(2.0f, 9f, 2.0f), wallMat, geo.transform);

            // Throne Platform
            CreateCube("Throne_Dais", new Vector3(0f, 0.4f, 9.5f), new Vector3(6f, 0.8f, 4f), wallMat, geo.transform);

            // Ominous Crimson & Gold Lighting
            CreatePointLight("CrownLight_Center", wing.transform, new Vector3(0f, 6.0f, 2f), new Color(1f, 0.35f, 0.15f), 28f, 3.0f);
            CreatePointLight("CrownLight_Throne", wing.transform, new Vector3(0f, 4.0f, 9f), new Color(1f, 0.8f, 0.2f), 18f, 2.5f);

            // Player Spawn & Return
            GameObject spawnPoint = new GameObject("CrownHall_PlayerSpawnPoint");
            spawnPoint.transform.SetParent(wing.transform, false);
            spawnPoint.transform.localPosition = new Vector3(0f, 0.2f, -7.5f);

            GameObject returnPortal = new GameObject("CrownHall_ReturnPortal");
            returnPortal.transform.SetParent(wing.transform, false);
            returnPortal.transform.localPosition = new Vector3(0f, 0.5f, -11.0f);
            BoxCollider retCol = returnPortal.AddComponent<BoxCollider>();
            retCol.size = new Vector3(3f, 3f, 1.5f);
            retCol.isTrigger = true;
            DoorTeleporter retTeleport = returnPortal.AddComponent<DoorTeleporter>();
            retTeleport.PromptMessage = "Palaa Kirjastoon (Wing 2)";
            retTeleport.DestinationZone = "Library";
            retTeleport.TargetSpawnPosition = new Vector3(0f, -69.8f, 7.0f);

            // Tactical Grid
            if (gridPrefab != null)
            {
                GameObject grid = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, wing.transform);
                grid.name = "CombatGrid_CrownHall";
                grid.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            }

            // Boss: The Gargoyle King
            GameObject enemiesContainer = new GameObject("Enemies");
            enemiesContainer.transform.SetParent(wing.transform, false);

            List<GameObject> enemyList = new List<GameObject>();
            GameObject bossObj = CreateBossUnit<GargoyleKingBoss>("Boss_GargoyleKing", new Vector3(0f, 1.3f, 6.5f), wallMat, goldMat, enemiesContainer.transform);
            bossObj.SetActive(false);
            enemyList.Add(bossObj);

            // Pre-combat Dialogue NPC
            CreateBossDialogueNPC("NPC_GargoyleKing", new Vector3(0f, 1.2f, 2.5f), wallMat, "Assets/Data/Dialogues/GargoyleKing_Intro.asset", "Haasta Kivettymiskuningas", wing.transform);

            // Royal Treasure Chest (100 Gold + Campaign Victory)
            GameObject chestObj = CreateRewardChest("CrownHall_Treasure_Chest", new Vector3(0f, 1.2f, 10.0f), woodMat, goldMat, chestPrefab, wing.transform, 100);
            chestObj.SetActive(false);

            // Room Controller
            GameObject triggerObj = new GameObject("CrownHall_Encounter_Trigger");
            triggerObj.transform.SetParent(wing.transform, false);
            triggerObj.transform.localPosition = new Vector3(0f, 3.0f, 0f);
            BoxCollider trigCol = triggerObj.AddComponent<BoxCollider>();
            trigCol.size = new Vector3(22f, 6f, 22f);
            trigCol.isTrigger = true;

            DungeonRoomController room = triggerObj.AddComponent<DungeonRoomController>();
            room.roomLocation = "CrownHall";
            room.bossIdentifier = "GargoyleKing";
            room.roomEnemies = enemyList;
            room.secretPassageOrChest = chestObj;
            room.exitBarriers = new List<GameObject>();
        }

        #endregion

        #region Helpers

        private static GameObject CreateCube(string name, Vector3 localPos, Vector3 scale, Material mat, Transform parent)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = scale;
            if (mat != null)
            {
                Renderer rend = obj.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = mat;
            }
            return obj;
        }

        private static GameObject CreatePointLight(string name, Transform parent, Vector3 localPos, Color color, float range, float intensity)
        {
            GameObject lightObj = new GameObject(name);
            lightObj.transform.SetParent(parent, false);
            lightObj.transform.localPosition = localPos;
            Light lt = lightObj.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = color;
            lt.range = range;
            lt.intensity = intensity;
            lt.shadows = LightShadows.Soft;
            return lightObj;
        }

        private static GameObject CreateEnemyUnit(string name, Vector3 localPos, Material bodyMat, Material eyeMat, Transform parent, int hp, int ac, int dmg)
        {
            GameObject enemyObj = new GameObject(name);
            enemyObj.transform.SetParent(parent, false);
            enemyObj.transform.localPosition = localPos;

            // Visual body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(enemyObj.transform, false);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            if (bodyMat != null) body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            // CombatUnit component
            EnemyUnit enemyComp = enemyObj.AddComponent<EnemyUnit>();
            CapsuleCollider col = enemyObj.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.5f;
            col.center = new Vector3(0f, 0.9f, 0f);

            return enemyObj;
        }

        private static GameObject CreateBossUnit<T>(string name, Vector3 localPos, Material bodyMat, Material accentMat, Transform parent) where T : EnemyUnit
        {
            GameObject bossObj = new GameObject(name);
            bossObj.transform.SetParent(parent, false);
            bossObj.transform.localPosition = localPos;

            // Visual imposing body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "BossBody";
            body.transform.SetParent(bossObj.transform, false);
            body.transform.localScale = new Vector3(1.3f, 1.4f, 1.3f);
            if (bodyMat != null) body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crown.name = "BossCrown";
            crown.transform.SetParent(bossObj.transform, false);
            crown.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            crown.transform.localScale = new Vector3(0.7f, 0.15f, 0.7f);
            if (accentMat != null) crown.GetComponent<Renderer>().sharedMaterial = accentMat;

            bossObj.AddComponent<T>();
            CapsuleCollider col = bossObj.AddComponent<CapsuleCollider>();
            col.height = 2.4f;
            col.radius = 0.7f;
            col.center = new Vector3(0f, 1.2f, 0f);

            return bossObj;
        }

        private static GameObject CreateBossDialogueNPC(string name, Vector3 localPos, Material mat, string dialogueAssetPath, string prompt, Transform parent)
        {
            GameObject npcObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npcObj.name = name;
            npcObj.transform.SetParent(parent, false);
            npcObj.transform.localPosition = localPos;
            npcObj.transform.localScale = new Vector3(1.1f, 1.2f, 1.1f);

            if (mat != null) npcObj.GetComponent<Renderer>().sharedMaterial = mat;

            VillageNPC vNPC = npcObj.AddComponent<VillageNPC>();
            vNPC.PromptMessage = prompt;
            vNPC.InteractionRadius = 4.5f;
            vNPC.IsInteractable = true;
            vNPC.StartingDialogueNode = AssetDatabase.LoadAssetAtPath<DialogueNodeSO>(dialogueAssetPath);

            return npcObj;
        }

        private static GameObject CreateRewardChest(string name, Vector3 localPos, Material woodMat, Material goldMat, GameObject chestPrefab, Transform parent, int gold)
        {
            GameObject chestObj;
            if (chestPrefab != null)
            {
                chestObj = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, parent);
                chestObj.name = name;
                chestObj.transform.localPosition = localPos;
            }
            else
            {
                chestObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chestObj.name = name;
                chestObj.transform.SetParent(parent, false);
                chestObj.transform.localPosition = localPos;
                chestObj.transform.localScale = new Vector3(1.4f, 0.9f, 1.0f);
                if (woodMat != null) chestObj.GetComponent<Renderer>().sharedMaterial = woodMat;
            }

            BoxCollider col = chestObj.GetComponent<BoxCollider>() ?? chestObj.AddComponent<BoxCollider>();
            col.size = new Vector3(1.4f, 0.9f, 1.0f);
            col.center = new Vector3(0f, 0.45f, 0f);

            ChestRewardInteraction reward = chestObj.GetComponent<ChestRewardInteraction>() ?? chestObj.AddComponent<ChestRewardInteraction>();
            reward.GoldReward = gold;

            return chestObj;
        }

        #endregion
    }
}
