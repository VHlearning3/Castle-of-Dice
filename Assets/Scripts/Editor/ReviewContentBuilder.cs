using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Adds the critical review's content to zones 2-7 under one "Review_Content" root per scene, next to the
    /// Zone_Dressing decor and the gameplay objects (which it never touches):
    /// letters and diaries (C3), Pip the trapped peddler, the ghost guard and the vault in the Castle Hall (C4),
    /// the Tower challenge with its rune pillars, traps and mimic guardian (C5), and the Forest Path's second
    /// encounter with a skeleton archer and a curse cultist (C6, C7). Running it again rebuilds only that root.
    /// Also makes the assets the content needs: the explosive barrel prefab (B6) and Shadow Step's icon (A4).
    /// Zone 1 is never opened (its notes are placed at runtime by RuntimeLoreSpawner).
    /// Batch: -executeMethod CastleOfTheD20.Editor.ReviewContentBuilder.BatchBuildAndScreenshots
    /// </summary>
    public static class ReviewContentBuilder
    {
        private const string RootName = "Review_Content";
        private const string MatDir = "Assets/Materials/ReviewContent";
        private const string ScreenshotDir = "Screenshots/ReviewContent";

        private static Transform root;
        private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        #region Entry points

        [MenuItem("CastleOfDice/Add Review Content (Zones 2-7)", false, 13)]
        public static void BuildAll()
        {
            BuildAssets();
            Build("Zone_2_ForestPath", BuildForest);
            Build("Zone_3_CastleCourtyard", BuildCourtyard);
            Build("Zone_4_Library", BuildLibrary);
            Build("Zone_5_CastleHall", BuildHall);
            Build("Zone_6_Tower", BuildTower);
            Build("Zone_7_ThroneRoom", BuildThroneRoom);
            AssetDatabase.SaveAssets();
            Debug.Log("[ReviewContentBuilder] Review content added to zones 2-7.");
        }

        public static void BatchBuildAndScreenshots()
        {
            BuildAll();
            BatchScreenshots();
        }

        private static void Build(string sceneName, System.Action build)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == RootName) Object.DestroyImmediate(go);
            }
            Physics.SyncTransforms();
            root = new GameObject(RootName).transform;
            build();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        #endregion

        #region Shared assets

        /// <summary>The explosive barrel prefab (B6) and Shadow Step's own icon (A4).</summary>
        public static void BuildAssets()
        {
            Directory.CreateDirectory(MatDir);
            BuildBarrelPrefab();
            BuildShadowStepIcon();
        }

        private static void BuildBarrelPrefab()
        {
            const string path = "Assets/Resources/Combat/ExplosiveBarrel.prefab";
            Directory.CreateDirectory("Assets/Resources/Combat");
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Barrel.prefab");

            GameObject barrel = new GameObject("Explosive_Barrel");
            if (source != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, barrel.transform);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Barrel_Model";
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

                // Fit the barrel inside one 1.6 m tile and paint it red
                Bounds b = RendererBounds(model);
                float scale = b.size.y > 0.01f ? 1.3f / b.size.y : 1f;
                model.transform.localScale = Vector3.one * scale;
                b = RendererBounds(model);
                model.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                Material red = Mat("ExplosiveBarrel_Red", new Color(0.62f, 0.12f, 0.08f), new Color(0.25f, 0.03f, 0f));
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    Material[] shared = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < shared.Length; i++) shared[i] = red;
                    r.sharedMaterials = shared;
                }
            }

            BoxCollider col = barrel.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.65f, 0f);
            col.size = new Vector3(0.9f, 1.3f, 0.9f);
            barrel.AddComponent<ExplosiveBarrel>();
            PrefabUtility.SaveAsPrefabAsset(barrel, path);
            Object.DestroyImmediate(barrel);
        }

        private static void BuildShadowStepIcon()
        {
            const string iconPath = "Assets/UI/Sprites/UI_Icon_ShadowStep.png";
            Texture2D blink = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Sprites/UI_Icon_Blink.png");
            if (blink == null) return;

            RenderTexture rt = RenderTexture.GetTemporary(blink.width, blink.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(blink, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D copy = new Texture2D(blink.width, blink.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, blink.width, blink.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            // Shadowy violet instead of Blink's arcane blue, mirrored so the two read differently
            Color32[] px = copy.GetPixels32();
            Color32[] outPx = new Color32[px.Length];
            int w = copy.width;
            for (int y = 0; y < copy.height; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = px[y * w + (w - 1 - x)];
                    float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    Color tinted = new Color(0.35f + lum * 0.5f, 0.12f + lum * 0.35f, 0.45f + lum * 0.55f, c.a);
                    outPx[y * w + x] = tinted;
                }
            }
            copy.SetPixels32(outPx);
            copy.Apply();
            File.WriteAllBytes(iconPath, copy.EncodeToPNG());
            Object.DestroyImmediate(copy);
            AssetDatabase.ImportAsset(iconPath);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            AbilitySO step = AssetDatabase.LoadAssetAtPath<AbilitySO>("Assets/Data/Ability_Rogue_ShadowStep.asset");
            if (icon != null && step != null)
            {
                SerializedObject so = new SerializedObject(step);
                so.FindProperty("abilityIcon").objectReferenceValue = icon;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(step);
            }
        }

        #endregion

        #region Zones

        private static void BuildForest()
        {
            Note("forest_patrol", new Vector3(3.5f, 0f, -45f));
            Note("forest_hunter", new Vector3(-21f, 0f, 11f));

            // Second encounter on the north half of the path (C6): a skeleton archer and a curse cultist (C7)
            const float z = 24f;
            Transform encounter = Group("Forest_Ambush_Encounter");
            GameObject grid = InstantiatePrefab("Assets/PREFABS/CombatGrid.prefab", encounter, new Vector3(0f, 0.05f, z));
            if (grid != null) grid.name = "CombatGrid_ForestAmbush";

            EnemyUnit archer = BuildSkeletonArcher(encounter, Free(new Vector3(-3f, 0f, z + 5f), 0.8f));
            CultistCaster cultist = BuildCultist(encounter, Free(new Vector3(3.5f, 0f, z + 6f), 0.8f));

            DungeonRoomController room = Room(encounter, "Forest_Ambush_Trigger", new Vector3(0f, 2.5f, z), new Vector3(22f, 6f, 12f), "Forest", "ForestAmbush");
            room.roomEnemies = new List<GameObject> { archer.gameObject, cultist.gameObject };
        }

        private static void BuildCourtyard()
        {
            Note("courtyard_oath", new Vector3(6f, 0f, -34f));
            Note("courtyard_roster", new Vector3(-12f, 0f, 37f));
        }

        private static void BuildLibrary()
        {
            Note("library_journal", new Vector3(-30f, 0f, 7f));
            Note("library_counterrune", new Vector3(30f, 0f, -7f));
        }

        private static void BuildHall()
        {
            Note("hall_decree", new Vector3(5.5f, 0f, -29f));
            Note("hall_diary", new Vector3(-27f, 0f, -21f));

            // Pip the Peddler, trapped since the curse (second shop)
            BuildScriptedNpc(ScriptedNpc.Kind.TrappedMerchant, "Pip_The_Peddler", "Assets/PREFABS/NPCs/NPC_Barnaby_3dmodel.prefab",
                Free(new Vector3(-27f, 0f, -12f), 0.9f), 90f, null);

            // The Queen's treasury vault and its ghost guard
            Transform vaultRoot = Group("Queens_Vault");
            Vector3 doorPos = Free(new Vector3(37.6f, 0f, -24f), 1.6f, 4f);
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Vault_Door";
            door.transform.SetParent(vaultRoot, false);
            door.transform.position = doorPos + new Vector3(0f, 2f, 0f);
            door.transform.localScale = new Vector3(0.6f, 4f, 3.2f);
            door.GetComponent<Renderer>().sharedMaterial = Mat("Vault_Iron", new Color(0.22f, 0.22f, 0.26f), new Color(0f, 0.05f, 0.08f));

            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Chest.prefab");
            GameObject chest = new GameObject("Vault_Reward_Chest");
            chest.transform.SetParent(vaultRoot, false);
            chest.transform.position = doorPos + new Vector3(-2.2f, 0f, 0f);
            chest.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            if (chestPrefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, chest.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
            }
            ChestRewardInteraction reward = chest.AddComponent<ChestRewardInteraction>();
            reward.GoldReward = 80;
            SerializedObject rso = new SerializedObject(reward);
            SerializedProperty item = rso.FindProperty("itemReward");
            if (item != null) item.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_RerollRuneStone.asset");
            rso.ApplyModifiedPropertiesWithoutUndo();
            chest.SetActive(false);

            HallVault vault = vaultRoot.gameObject.AddComponent<HallVault>();
            vault.Door = door;
            vault.RewardChest = chest;

            Vector3 ghostPos = Free(doorPos + new Vector3(-3f, 0f, 4f), 0.9f);
            BuildScriptedNpc(ScriptedNpc.Kind.GhostGuard, "Ghost_Of_Sir_Aldric", "Assets/PREFABS/Characters/Visual_Enemy_Skeleton.prefab",
                ghostPos, -90f, Mat("Ghost_Pale", new Color(0.55f, 0.8f, 1f), new Color(0.25f, 0.55f, 0.85f)));
        }

        private static void BuildTower()
        {
            Note("tower_inventory", new Vector3(-9f, 0f, -6f));
            Note("tower_warning", new Vector3(8f, 0f, -7f));

            Transform challengeRoot = Group("Tower_Challenge");
            TowerChallenge challenge = challengeRoot.gameObject.AddComponent<TowerChallenge>();

            // Three rune pillars behind the pedestal: SUN, MOON, STAR left to right facing the vial
            Material stone = Mat("RunePillar_Stone", new Color(0.45f, 0.43f, 0.4f), Color.black);
            Material crystalMat = Mat("RunePillar_Crystal", new Color(0.7f, 0.3f, 0.9f), new Color(0.5f, 0.2f, 0.7f));
            float[] xs = { -6f, 0f, 6f };
            for (int i = 0; i < xs.Length; i++)
            {
                Vector3 pos = Free(new Vector3(xs[i], 0f, 10f), 0.9f);
                challenge.Pillars.Add(BuildRunePillar(challengeRoot, "Rune_Pillar_" + (i + 1), pos, stone, crystalMat));
            }

            // Seal over the elixir and the ring until the challenge is done
            GameObject pedestal = GameObject.Find("GiantElixir_Pedestal");
            GameObject ring = GameObject.Find("SignetRing_Pickup");
            Vector3 sealCenter = pedestal != null ? pedestal.transform.position : new Vector3(0f, 0.5f, 4f);
            if (pedestal != null)
            {
                Interactable elixir = pedestal.GetComponent<GiantElixirInteraction>();
                if (elixir != null) challenge.SealedRewards.Add(elixir);
            }
            if (ring != null)
            {
                Interactable ringReward = ring.GetComponent<ChestRewardInteraction>();
                if (ringReward != null) challenge.SealedRewards.Add(ringReward);
            }
            challenge.SealVisual = BuildSeal(challengeRoot, new Vector3(sealCenter.x, 0f, sealCenter.z));

            // Pressure plates across the way in
            Material plateMat = Mat("Trap_Plate", new Color(0.3f, 0.28f, 0.26f), Color.black);
            BuildPlate(challengeRoot, "Trap_Plate_1", new Vector3(0f, 0f, -4.5f), plateMat);
            BuildPlate(challengeRoot, "Trap_Plate_2", new Vector3(-2.6f, 0f, -1.2f), plateMat);
            BuildPlate(challengeRoot, "Trap_Plate_3", new Vector3(2.6f, 0f, -1.2f), plateMat);

            // The treasure guardian: a mimic waiting in the corner as a chest
            Transform encounter = Group("Tower_Guardian_Encounter");
            GameObject grid = InstantiatePrefab("Assets/PREFABS/CombatGrid.prefab", encounter, new Vector3(0f, 0.05f, 0f));
            if (grid != null) grid.name = "CombatGrid_Tower";

            Vector3 mimicPos = Free(new Vector3(6f, 0f, 4f), 0.9f);
            MimicUnit mimic = BuildMimic(encounter, mimicPos);
            DungeonRoomController room = Room(encounter, "Tower_Guardian_Room", mimicPos + new Vector3(0f, 1f, 0f), new Vector3(1.2f, 2f, 1.2f), "Tower", "TowerGuardian");
            room.roomEnemies = new List<GameObject> { mimic.gameObject };
            room.gridAreaXZ = new Rect(-13f, -13f, 26f, 26f);
            challenge.GuardianRoom = room;

            GameObject prop = new GameObject("Mimic_Disguise");
            prop.transform.SetParent(encounter, false);
            prop.transform.position = mimicPos;
            prop.transform.rotation = Quaternion.Euler(0f, 181f, 0f);
            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Chest.prefab");
            if (chestPrefab != null)
            {
                GameObject disguiseModel = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, prop.transform);
                disguiseModel.transform.localPosition = Vector3.zero;
                disguiseModel.transform.localRotation = Quaternion.identity;
            }
            BoxCollider propCol = prop.AddComponent<BoxCollider>();
            propCol.center = new Vector3(0f, 0.6f, 0f);
            propCol.size = new Vector3(1.6f, 1.2f, 1.2f);
            MimicChest disguise = prop.AddComponent<MimicChest>();
            disguise.GuardianRoom = room;
            disguise.PropChest = prop;
        }

        private static void BuildThroneRoom()
        {
            Note("throne_queen", new Vector3(-12f, 0f, -38f));
            Note("throne_lastwords", new Vector3(12f, 0f, 25f));
        }

        #endregion

        #region Builders

        private static void Note(string id, Vector3 preferred)
        {
            Vector3 pos = Free(preferred, 0.5f);
            LoreNote note = LoreNote.Create(id, pos, 0f, Group("Lore_Notes"));
            Renderer paper = note.GetComponentInChildren<Renderer>();
            if (paper != null) paper.sharedMaterial = Mat("Parchment", new Color(0.86f, 0.78f, 0.6f), new Color(0.12f, 0.1f, 0.05f));
            SerializedObject so = new SerializedObject(note);
            so.FindProperty("noteId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static EnemyUnit BuildSkeletonArcher(Transform parent, Vector3 floorPos)
        {
            GameObject archer = InstantiatePrefab("Assets/PREFABS/Enemies/Enemy_SkeletonGuard.prefab", parent, floorPos + new Vector3(0f, 1.5f, 0f));
            archer.name = "Forest_Skeleton_Archer";
            archer.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            EnemyUnit unit = archer.GetComponent<EnemyUnit>();
            SerializedObject so = new SerializedObject(unit);
            so.FindProperty("unitName").stringValue = "Skeleton Archer";
            so.FindProperty("maxHP").intValue = 16;
            so.FindProperty("currentHP").intValue = 16;
            so.FindProperty("armorClass").intValue = 12;
            so.FindProperty("attackDamage").intValue = 4;
            so.FindProperty("attackBonus").intValue = 4;
            so.FindProperty("attackRange").intValue = 5;
            so.FindProperty("damageDiceCount").intValue = 1;
            so.FindProperty("damageDiceSides").intValue = 8;
            so.FindProperty("damageDiceBonus").intValue = 0; // 1d8 (typical hit 4): not elite, so both ambushers fight
            so.FindProperty("initiativeBonus").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            archer.SetActive(false);
            return unit;
        }

        private static CultistCaster BuildCultist(Transform parent, Vector3 floorPos)
        {
            const float height = 2.6f;
            GameObject cultist = new GameObject("Forest_Curse_Cultist");
            cultist.tag = "Enemy";
            cultist.transform.SetParent(parent, false);
            cultist.transform.position = floorPos + new Vector3(0f, height * 0.5f, 0f);
            cultist.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            CapsuleCollider col = cultist.AddComponent<CapsuleCollider>();
            col.height = height;
            col.radius = 0.55f;
            col.center = Vector3.zero;
            CultistCaster caster = cultist.AddComponent<CultistCaster>();

            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Characters/Visual_Boss_Malakor.prefab");
            if (visualPrefab != null)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, cultist.transform);
                visual.name = "Visuals";
                visual.transform.localPosition = new Vector3(0f, -height * 0.5f, 0f);
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one * 0.8f;

                // Dark red robes, so the cultist never reads as Malakor himself
                Material robe = Mat("Cultist_Robe", new Color(0.35f, 0.06f, 0.08f), new Color(0.08f, 0f, 0f));
                foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
                {
                    Material[] shared = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < shared.Length; i++) shared[i] = robe;
                    r.sharedMaterials = shared;
                }
            }

            SerializedObject so = new SerializedObject(caster);
            so.FindProperty("unitName").stringValue = "Curse Cultist";
            so.FindProperty("maxHP").intValue = 18;
            so.FindProperty("currentHP").intValue = 18;
            so.FindProperty("armorClass").intValue = 11;
            so.FindProperty("attackBonus").intValue = 3;
            so.FindProperty("initiativeBonus").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            cultist.SetActive(false);
            return caster;
        }

        private static MimicUnit BuildMimic(Transform parent, Vector3 floorPos)
        {
            const float height = 1.4f;
            GameObject mimic = new GameObject("Tower_Mimic");
            mimic.tag = "Enemy";
            mimic.transform.SetParent(parent, false);
            mimic.transform.position = floorPos + new Vector3(0f, height * 0.5f, 0f);
            mimic.transform.rotation = Quaternion.Euler(0f, 181f, 0f);
            BoxCollider col = mimic.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = new Vector3(1.4f, height, 1.1f);
            MimicUnit unit = mimic.AddComponent<MimicUnit>();

            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Chest.prefab");
            if (chestPrefab != null)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, mimic.transform);
                visual.name = "Visuals";
                visual.transform.localPosition = new Vector3(0f, -height * 0.5f, 0f);
                visual.transform.localRotation = Quaternion.identity;
                foreach (Collider c in visual.GetComponentsInChildren<Collider>()) c.enabled = false;
            }

            // A red tongue so the guardian reads as alive once it wakes
            GameObject tongue = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tongue.name = "Mimic_Tongue";
            Object.DestroyImmediate(tongue.GetComponent<Collider>());
            tongue.transform.SetParent(mimic.transform, false);
            tongue.transform.localPosition = new Vector3(0f, 0.1f, 0.6f);
            tongue.transform.localScale = new Vector3(0.35f, 0.08f, 0.7f);
            tongue.GetComponent<Renderer>().sharedMaterial = Mat("Mimic_Tongue", new Color(0.7f, 0.12f, 0.2f), new Color(0.2f, 0f, 0.05f));

            mimic.SetActive(false);
            return unit;
        }

        private static void BuildScriptedNpc(ScriptedNpc.Kind kind, string name, string modelPrefabPath, Vector3 floorPos, float yaw, Material tint)
        {
            const float height = 2.8f;
            GameObject npc = new GameObject(name);
            npc.transform.SetParent(root, false);
            npc.transform.position = floorPos + new Vector3(0f, height * 0.5f, 0f);
            npc.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            CapsuleCollider col = npc.AddComponent<CapsuleCollider>();
            col.height = height;
            col.radius = 0.7f;
            col.center = Vector3.zero;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPrefabPath);
            if (model != null)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, npc.transform);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                visual.name = "Visuals";
                visual.transform.localPosition = new Vector3(0f, -height * 0.5f, 0f);
                visual.transform.localRotation = Quaternion.identity;

                // Only the model: no villager script, collider or quest marker from the source prefab
                foreach (MonoBehaviour mb in visual.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    string ns = mb.GetType().Namespace ?? "";
                    if (ns.StartsWith("CastleOfTheD20")) Object.DestroyImmediate(mb);
                }
                foreach (Collider c in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                if (tint != null)
                {
                    foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
                    {
                        Material[] shared = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < shared.Length; i++) shared[i] = tint;
                        r.sharedMaterials = shared;
                    }
                }
            }

            ScriptedNpc scripted = npc.AddComponent<ScriptedNpc>();
            scripted.NpcKind = kind;
            if (kind == ScriptedNpc.Kind.GhostGuard)
            {
                GameObject glowObj = new GameObject("Ghost_Glow");
                glowObj.transform.SetParent(npc.transform, false);
                glowObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                Light glow = glowObj.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(0.5f, 0.8f, 1f);
                glow.range = 6f;
                glow.intensity = 2.2f;
            }
        }

        private static RunePillar BuildRunePillar(Transform parent, string name, Vector3 floorPos, Material stone, Material crystalMat)
        {
            GameObject pillar = new GameObject(name);
            pillar.transform.SetParent(parent, false);
            pillar.transform.position = floorPos;

            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Column";
            column.transform.SetParent(pillar.transform, false);
            column.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            column.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
            column.GetComponent<Renderer>().sharedMaterial = stone;

            GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.name = "Rune_Crystal";
            Object.DestroyImmediate(crystal.GetComponent<Collider>());
            crystal.transform.SetParent(pillar.transform, false);
            crystal.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            crystal.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            crystal.transform.localScale = Vector3.one * 0.55f;
            Renderer crystalRenderer = crystal.GetComponent<Renderer>();
            crystalRenderer.sharedMaterial = crystalMat;

            GameObject labelObj = new GameObject("Rune_Label");
            labelObj.transform.SetParent(pillar.transform, false);
            labelObj.transform.localPosition = new Vector3(0f, 3.4f, 0f);
            labelObj.transform.localRotation = Quaternion.identity;
            TextMeshPro label = labelObj.AddComponent<TextMeshPro>();
            label.text = "EYE";
            label.fontSize = 6f;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.rectTransform.sizeDelta = new Vector2(4f, 1.2f);

            GameObject glowObj = new GameObject("Rune_Glow");
            glowObj.transform.SetParent(pillar.transform, false);
            glowObj.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            Light glow = glowObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 4f;
            glow.intensity = 1.6f;

            RunePillar rune = pillar.AddComponent<RunePillar>();
            rune.Label = label;
            rune.Crystal = crystalRenderer;
            rune.Glow = glow;
            return rune;
        }

        private static GameObject BuildSeal(Transform parent, Vector3 floorPos)
        {
            GameObject seal = new GameObject("Pedestal_Seal");
            seal.transform.SetParent(parent, false);
            seal.transform.position = floorPos;

            GameObject ringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringObj.name = "Seal_Circle";
            Object.DestroyImmediate(ringObj.GetComponent<Collider>());
            ringObj.transform.SetParent(seal.transform, false);
            ringObj.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ringObj.transform.localScale = new Vector3(4.6f, 0.02f, 4.6f);
            ringObj.GetComponent<Renderer>().sharedMaterial = Mat("Seal_Glow", new Color(0.45f, 0.2f, 0.8f), new Color(0.6f, 0.25f, 1f));

            GameObject labelObj = new GameObject("Seal_Label");
            labelObj.transform.SetParent(seal.transform, false);
            labelObj.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            labelObj.transform.localRotation = Quaternion.identity;
            TextMeshPro label = labelObj.AddComponent<TextMeshPro>();
            label.text = "SEALED";
            label.fontSize = 5f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.8f, 0.6f, 1f);
            label.rectTransform.sizeDelta = new Vector2(5f, 1.2f);

            GameObject glowObj = new GameObject("Seal_Light");
            glowObj.transform.SetParent(seal.transform, false);
            glowObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            Light glow = glowObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.6f, 0.3f, 1f);
            glow.range = 6f;
            glow.intensity = 2f;
            return seal;
        }

        private static void BuildPlate(Transform parent, string name, Vector3 floorPos, Material mat)
        {
            GameObject plate = new GameObject(name);
            plate.transform.SetParent(parent, false);
            plate.transform.position = floorPos;
            BoxCollider trigger = plate.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.6f, 0f);
            trigger.size = new Vector3(1.3f, 1.2f, 1.3f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Plate_Stone";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(plate.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            visual.transform.localScale = new Vector3(1.25f, 0.06f, 1.25f);
            visual.GetComponent<Renderer>().sharedMaterial = mat;

            PressurePlateTrap trap = plate.AddComponent<PressurePlateTrap>();
            trap.PlateVisual = visual.transform;
        }

        private static DungeonRoomController Room(Transform parent, string name, Vector3 pos, Vector3 size, string location, string key)
        {
            GameObject trigger = new GameObject(name);
            trigger.transform.SetParent(parent, false);
            trigger.transform.position = pos;
            BoxCollider col = trigger.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = size;
            DungeonRoomController room = trigger.AddComponent<DungeonRoomController>();
            room.roomLocation = location;
            room.bossIdentifier = "";
            room.roomKey = key;
            return room;
        }

        #endregion

        #region Helpers

        private static Transform Group(string name)
        {
            Transform existing = root.Find(name);
            if (existing != null) return existing;
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        private static GameObject InstantiatePrefab(string path, Transform parent, Vector3 pos)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[ReviewContentBuilder] Missing prefab " + path);
                return null;
            }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            return go;
        }

        /// <summary>
        /// The nearest spot to <paramref name="preferred"/> (on a widening ring of 1 m steps) where a column of
        /// <paramref name="radius"/> from knee to head height touches no solid collider. Y is the floor there.
        /// </summary>
        private static Vector3 Free(Vector3 preferred, float radius, float maxSearch = 8f)
        {
            Physics.SyncTransforms();
            for (float r = 0f; r <= maxSearch; r += 1f)
            {
                int steps = r < 0.5f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r);
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    Vector3 p = preferred + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    float floorY = FloorY(p);
                    Vector3 center = new Vector3(p.x, floorY + 1.2f, p.z);
                    if (!Physics.CheckBox(center, new Vector3(radius, 0.9f, radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    {
                        return new Vector3(p.x, floorY, p.z);
                    }
                }
            }
            Debug.LogWarning($"[ReviewContentBuilder] No free spot near {preferred}; using it anyway.");
            return new Vector3(preferred.x, FloorY(preferred), preferred.z);
        }

        private static float FloorY(Vector3 p)
        {
            // The floor is the highest surface below knee height (tree crowns and roofs are ignored)
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, 8f, p.z), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                float y = hits[i].point.y;
                if (y < 0.8f && y > best) best = y;
            }
            return float.IsNegativeInfinity(best) ? 0f : best;
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static Material Mat(string name, Color baseColor, Color emission)
        {
            if (mats.TryGetValue(name, out Material cached) && cached != null) return cached;
            string path = $"{MatDir}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            mat.color = baseColor;
            if (emission.maxColorComponent > 0.001f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
            }
            EditorUtility.SetDirty(mat);
            mats[name] = mat;
            return mat;
        }

        #endregion

        #region Screenshots

        private struct Shot
        {
            public string Scene;
            public Vector3 Focus;
            public float Size;
            public string Name;
        }

        private static readonly Shot[] Shots =
        {
            new Shot { Scene = "Zone_2_ForestPath", Focus = new Vector3(0f, 0f, 24f), Size = 16f, Name = "forest_ambush" },
            new Shot { Scene = "Zone_5_CastleHall", Focus = new Vector3(0f, 0f, -10f), Size = 32f, Name = "castle_hall" },
            new Shot { Scene = "Zone_6_Tower", Focus = new Vector3(0f, 0f, 2f), Size = 15f, Name = "tower" },
        };

        /// <summary>Top-down and angled shots of the new content (batch mode, Screenshots/ReviewContent).</summary>
        public static void BatchScreenshots()
        {
            Directory.CreateDirectory(ScreenshotDir);
            foreach (Shot shot in Shots)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + shot.Scene + ".unity", OpenSceneMode.Single);
                RenderShot(shot, true);
                RenderShot(shot, false);
            }
        }

        private static void RenderShot(Shot shot, bool topDown)
        {
            GameObject camObj = new GameObject("ReviewShotCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            if (topDown)
            {
                cam.orthographic = true;
                cam.orthographicSize = shot.Size;
                camObj.transform.position = shot.Focus + new Vector3(0f, 40f, 0f);
                camObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                cam.fieldOfView = 50f;
                camObj.transform.position = shot.Focus + new Vector3(0f, shot.Size * 0.9f, -shot.Size * 1.3f);
                camObj.transform.LookAt(shot.Focus);
            }
            cam.farClipPlane = 200f;

            RenderTexture rt = new RenderTexture(1280, 800, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"{ScreenshotDir}/{shot.Name}_{(topDown ? "top" : "angle")}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camObj);
        }

        #endregion
    }
}
