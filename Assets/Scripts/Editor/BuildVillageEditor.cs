using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.World;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.UI;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Editor automation tool that constructs the village buildings, props, paths,
    /// castle iron gate, and places NPCs (Baldur, Barnaby, Othelia, Mirabel) according to the notebook.
    /// </summary>
    public static class BuildVillageEditor
    {
        [MenuItem("CastleOfDice/Build Complete Village Layout & NPCs", false, 10)]
        public static void BuildCompleteVillage()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.name != "StartVillage")
            {
                if (EditorUtility.DisplayDialog("Open StartVillage Scene?",
                    "StartVillage scene is required to build the village layout. Open it now?", "Open", "Cancel"))
                {
                    activeScene = EditorSceneManager.OpenScene("Assets/Scenes/StartVillage.unity", OpenSceneMode.Single);
                }
                else
                {
                    return;
                }
            }

            // 1. Ensure game data assets exist (Othelia, Mirabel, Quests, Items)
            GenerateGameDataEditor.GenerateAllGameAssets();

            Undo.SetCurrentGroupName("Build Complete Village Layout");
            int undoGroup = Undo.GetCurrentGroup();

            // 2. Materials & Prefabs
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Logs.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Ruined_walls.mat");
            Material stoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Rocks_1_2.mat")
                ?? woodMat;
            Material metalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_HammerJAanvil.mat")
                ?? woodMat;

            GameObject forgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Forge.prefab");
            GameObject anvilPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Anvil.prefab");
            GameObject awningPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Awning.prefab");
            GameObject tavernPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Tavern.prefab");
            GameObject witchHousePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Witch_house.prefab");
            GameObject townHallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Town_Hall.prefab")
                ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Large_house.prefab");
            GameObject wellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Well.prefab");
            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Arch.prefab");
            GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Wall_1.prefab");
            GameObject towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Tower.prefab");
            GameObject fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Stone_fence.prefab")
                ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Fence.prefab");
            GameObject cauldronPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Cauldron.prefab");
            GameObject streetLightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/street_light.prefab");
            GameObject tree1Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Tree_1.prefab");
            GameObject tree2Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Tree_2.prefab");

            // Clean up old Village_Layout container if present
            GameObject existingLayout = GameObject.Find("Village_Layout");
            if (existingLayout != null)
            {
                Undo.DestroyObjectImmediate(existingLayout);
            }

            GameObject villageRoot = new GameObject("Village_Layout");
            Undo.RegisterCreatedObjectUndo(villageRoot, "Create Village Layout");

            // ========================================================
            // A. BALDUR'S FORGE AREA (Alasin ja katos === Forge)
            // ========================================================
            GameObject forgeArea = new GameObject("Area_BaldursForge");
            forgeArea.transform.SetParent(villageRoot.transform, false);

            if (forgePrefab != null)
            {
                GameObject forge = (GameObject)PrefabUtility.InstantiatePrefab(forgePrefab, forgeArea.transform);
                forge.transform.position = new Vector3(18.5f, 0f, -4.5f);
                forge.transform.rotation = Quaternion.Euler(0f, 210f, 0f);
            }

            if (anvilPrefab != null)
            {
                GameObject anvil = (GameObject)PrefabUtility.InstantiatePrefab(anvilPrefab, forgeArea.transform);
                anvil.transform.position = new Vector3(16.2f, 0f, -6.8f);
                anvil.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            }

            if (awningPrefab != null)
            {
                GameObject awning = (GameObject)PrefabUtility.InstantiatePrefab(awningPrefab, forgeArea.transform);
                awning.transform.position = new Vector3(17.2f, 0f, -7.5f);
                awning.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            // Align NPC_Baldur
            GameObject baldur = GameObject.Find("NPC_Baldur");
            if (baldur != null)
            {
                baldur.transform.position = new Vector3(16.8f, 1.0f, -6.2f);
                baldur.transform.rotation = Quaternion.Euler(0f, 220f, 0f);
            }

            // ========================================================
            // B. INNKEEPER BARNABY'S TAVERN (Majatalo === Tavern)
            // ========================================================
            GameObject tavernArea = new GameObject("Area_BarnabysTavern");
            tavernArea.transform.SetParent(villageRoot.transform, false);

            if (tavernPrefab != null)
            {
                GameObject tavern = (GameObject)PrefabUtility.InstantiatePrefab(tavernPrefab, tavernArea.transform);
                tavern.transform.position = new Vector3(-4.5f, 0f, -0.5f);
                tavern.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }

            // Align NPC_Barnaby near cellar hatch
            GameObject barnaby = GameObject.Find("NPC_Barnaby");
            if (barnaby != null)
            {
                barnaby.transform.position = new Vector3(0.2f, 1.0f, -5.2f);
                barnaby.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
            }

            // ========================================================
            // C. HERBALIST MIRABEL'S WITCH HOUSE (Pieni aita vallihaudan suuntaan === Witch_house)
            // ========================================================
            GameObject mirabelArea = new GameObject("Area_MirabelsHut");
            mirabelArea.transform.SetParent(villageRoot.transform, false);

            if (witchHousePrefab != null)
            {
                GameObject hut = (GameObject)PrefabUtility.InstantiatePrefab(witchHousePrefab, mirabelArea.transform);
                hut.transform.position = new Vector3(-24.0f, 0f, 9.0f);
                hut.transform.rotation = Quaternion.Euler(0f, 120f, 0f);
            }

            if (fencePrefab != null)
            {
                // Enclosing fence towards the moat
                for (int i = 0; i < 4; i++)
                {
                    GameObject fence = (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab, mirabelArea.transform);
                    fence.transform.position = new Vector3(-20.0f + (i * 2.2f), 0f, 13.0f);
                    fence.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                }
            }

            if (cauldronPrefab != null)
            {
                GameObject cauldron = (GameObject)PrefabUtility.InstantiatePrefab(cauldronPrefab, mirabelArea.transform);
                cauldron.transform.position = new Vector3(-18.0f, 0f, 9.5f);
            }

            // Create NPC_Mirabel
            EnsureMirabelNPC(mirabelArea.transform);

            // ========================================================
            // D. ELDER OTHELIA'S TOWN HALL (Town_Hall / Kylänvanhin)
            // ========================================================
            GameObject otheliaArea = new GameObject("Area_TownHall_Othelia");
            otheliaArea.transform.SetParent(villageRoot.transform, false);

            if (townHallPrefab != null)
            {
                GameObject hall = (GameObject)PrefabUtility.InstantiatePrefab(townHallPrefab, otheliaArea.transform);
                hall.transform.position = new Vector3(16.0f, 0f, 15.0f);
                hall.transform.rotation = Quaternion.Euler(0f, 240f, 0f);
            }

            // Create NPC_Othelia
            EnsureOtheliaNPC(otheliaArea.transform);

            // ========================================================
            // E. VILLAGE SQUARE (Well, Streetlights & Trees)
            // ========================================================
            GameObject squareArea = new GameObject("Area_VillageSquare");
            squareArea.transform.SetParent(villageRoot.transform, false);

            if (wellPrefab != null)
            {
                GameObject well = (GameObject)PrefabUtility.InstantiatePrefab(wellPrefab, squareArea.transform);
                well.transform.position = new Vector3(4.5f, 0f, 3.5f);
            }

            if (streetLightPrefab != null)
            {
                Vector3[] lightPositions = new Vector3[]
                {
                    new Vector3(1.5f, 0f, -4.0f),
                    new Vector3(14.0f, 0f, -5.0f),
                    new Vector3(10.0f, 0f, 8.0f),
                    new Vector3(-14.0f, 0f, 6.0f),
                    new Vector3(3.0f, 0f, 22.0f)
                };
                foreach (var lp in lightPositions)
                {
                    GameObject lamp = (GameObject)PrefabUtility.InstantiatePrefab(streetLightPrefab, squareArea.transform);
                    lamp.transform.position = lp;
                }
            }

            if (tree1Prefab != null && tree2Prefab != null)
            {
                Vector3[] treePositions = new Vector3[]
                {
                    new Vector3(-10f, 0f, -8f),
                    new Vector3(-16f, 0f, -4f),
                    new Vector3(22f, 0f, 2f),
                    new Vector3(20f, 0f, 22f),
                    new Vector3(-25f, 0f, 20f),
                    new Vector3(-8f, 0f, 25f),
                    new Vector3(12f, 0f, 28f)
                };
                for (int i = 0; i < treePositions.Length; i++)
                {
                    GameObject tPrefab = (i % 2 == 0) ? tree1Prefab : tree2Prefab;
                    GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(tPrefab, squareArea.transform);
                    tree.transform.position = treePositions[i];
                    tree.transform.rotation = Quaternion.Euler(0f, i * 55f, 0f);
                }
            }

            // ========================================================
            // F. NORTHERN CASTLE IRON GATE (Tie, josta siirrytään linnaan)
            // ========================================================
            GameObject gateArea = new GameObject("Area_NorthernCastleGate");
            gateArea.transform.SetParent(villageRoot.transform, false);

            Vector3 gatePos = new Vector3(0f, 0f, 32.0f);

            if (archPrefab != null)
            {
                GameObject arch = (GameObject)PrefabUtility.InstantiatePrefab(archPrefab, gateArea.transform);
                arch.transform.position = gatePos;
                arch.transform.rotation = Quaternion.identity;
                arch.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            }

            if (wallPrefab != null)
            {
                GameObject wallL = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, gateArea.transform);
                wallL.transform.position = gatePos + new Vector3(-5.5f, 0f, 0f);
                wallL.transform.rotation = Quaternion.identity;

                GameObject wallR = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, gateArea.transform);
                wallR.transform.position = gatePos + new Vector3(5.5f, 0f, 0f);
                wallR.transform.rotation = Quaternion.identity;
            }

            if (towerPrefab != null)
            {
                GameObject towerL = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab, gateArea.transform);
                towerL.transform.position = gatePos + new Vector3(-10.5f, 0f, 0f);

                GameObject towerR = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab, gateArea.transform);
                towerR.transform.position = gatePos + new Vector3(10.5f, 0f, 0f);
            }

            // Add Gate Interaction Trigger
            GameObject gateTriggerObj = new GameObject("Castle_Gate_Portal");
            gateTriggerObj.transform.SetParent(gateArea.transform, false);
            gateTriggerObj.transform.position = gatePos + new Vector3(0f, 1.5f, 0f);

            BoxCollider gateCollider = gateTriggerObj.AddComponent<BoxCollider>();
            gateCollider.size = new Vector3(4.5f, 3.5f, 2.0f);
            gateCollider.isTrigger = true;

            DoorTeleporter gateTeleporter = gateTriggerObj.AddComponent<DoorTeleporter>();
            gateTeleporter.PromptMessage = "Astu Linnan Alapihalle (Wing 1: Courtyard)";
            gateTeleporter.DestinationZone = "Courtyard";
            gateTeleporter.InteractionRadius = 4.5f;

            // ========================================================
            // G. ENSURE PERSISTENT CORE MANAGERS IN SCENE
            // ========================================================
            EnsureManagersInScene();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("[BuildVillageEditor] Complete village layout successfully created and saved in StartVillage.unity!");
        }

        private static void EnsureMirabelNPC(Transform parent)
        {
            GameObject mirabel = GameObject.Find("NPC_Mirabel");
            if (mirabel == null)
            {
                mirabel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                mirabel.name = "NPC_Mirabel";
                Undo.RegisterCreatedObjectUndo(mirabel, "Create NPC_Mirabel");
            }

            mirabel.transform.SetParent(parent, false);
            mirabel.transform.position = new Vector3(-18.5f, 1.0f, 8.5f);
            mirabel.transform.rotation = Quaternion.Euler(0f, 130f, 0f);

            // Capsule styling
            Renderer rend = mirabel.GetComponent<Renderer>();
            Material herbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Fern.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Tree_1.mat");
            if (rend != null && herbMat != null)
            {
                rend.sharedMaterial = herbMat;
            }

            VillageNPC vNPC = mirabel.GetComponent<VillageNPC>();
            if (vNPC == null)
            {
                vNPC = mirabel.AddComponent<VillageNPC>();
            }
            vNPC.PromptMessage = "Puhu Yrttiparantaja Mirabelille";
            vNPC.InteractionRadius = 5.0f;
            vNPC.IsInteractable = true;
            vNPC.StartingDialogueNode = AssetDatabase.LoadAssetAtPath<DialogueNodeSO>("Assets/Data/Dialogues/Mirabel_Intro.asset");
        }

        private static void EnsureOtheliaNPC(Transform parent)
        {
            GameObject othelia = GameObject.Find("NPC_Othelia");
            if (othelia == null)
            {
                othelia = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                othelia.name = "NPC_Othelia";
                Undo.RegisterCreatedObjectUndo(othelia, "Create NPC_Othelia");
            }

            othelia.transform.SetParent(parent, false);
            othelia.transform.position = new Vector3(11.5f, 1.0f, 12.5f);
            othelia.transform.rotation = Quaternion.Euler(0f, 230f, 0f);

            Renderer rend = othelia.GetComponent<Renderer>();
            Material elderMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Chest.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyVillageAll/Omat Materials/M_Rocks_1_2.mat");
            if (rend != null && elderMat != null)
            {
                rend.sharedMaterial = elderMat;
            }

            VillageNPC vNPC = othelia.GetComponent<VillageNPC>();
            if (vNPC == null)
            {
                vNPC = othelia.AddComponent<VillageNPC>();
            }
            vNPC.PromptMessage = "Puhu Kylänvanhin Othelialle";
            vNPC.InteractionRadius = 5.0f;
            vNPC.IsInteractable = true;
            vNPC.StartingDialogueNode = AssetDatabase.LoadAssetAtPath<DialogueNodeSO>("Assets/Data/Dialogues/Othelia_Intro.asset");
        }

        private static void EnsureManagersInScene()
        {
            GameObject managers = GameObject.Find("Managers");
            if (managers == null)
            {
                managers = new GameObject("Managers");
                Undo.RegisterCreatedObjectUndo(managers, "Create Managers");
            }

            if (managers.GetComponent<AudioManager>() == null && Object.FindAnyObjectByType<AudioManager>() == null)
            {
                managers.AddComponent<AudioManager>();
            }

            if (managers.GetComponent<DialogueActionTrigger>() == null && Object.FindAnyObjectByType<DialogueActionTrigger>() == null)
            {
                managers.AddComponent<DialogueActionTrigger>();
            }

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                if (canvas.GetComponent<MainMenuController>() == null && Object.FindAnyObjectByType<MainMenuController>() == null)
                {
                    canvas.gameObject.AddComponent<MainMenuController>();
                }
                if (canvas.GetComponent<DefeatUIController>() == null && Object.FindAnyObjectByType<DefeatUIController>() == null)
                {
                    canvas.gameObject.AddComponent<DefeatUIController>();
                }
            }
        }
    }
}
