using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Places the Rogue's lockpick practice chest in the village (Zone 1), next to Baldur's forge.
    /// It is an easy lock (DC 8, no trap, a little gold) so the Rogue learns the minigame
    /// before the Forest Path secret gate. Safe to run again: an existing chest keeps its position.
    /// </summary>
    public static class LockpickPracticeChestEditor
    {
        public const string ChestName = "Village_Lockpick_Practice_Chest";
        private const string Zone1Path = "Assets/Scenes/Zone_1_VillageAndCellar.unity";
        private const string ChestPrefabPath = "Assets/PREFABS/Chest.prefab";
        private const string LidPrefabPath = "Assets/PREFABS/Chest_cover.prefab";
        private const string LidName = "Chest_Lid";

        // The cover's pivot is its hinge edge; the lid body extends along its local -Z, so +X tips it up
        private static readonly Vector3 OpenLidRotation = new Vector3(70f, 0f, 0f);

        // Beside Baldur (south-west of him), clear of the forge, anvil and awning
        private static readonly Vector3 OffsetFromBaldur = new Vector3(-3.0f, 0f, -2.6f);
        private static readonly Vector3 FallbackPosition = new Vector3(13.8f, 0f, -8.8f);

        [MenuItem("CastleOfDice/Add Lockpick Practice Chest (Village)", false, 12)]
        public static void AddPracticeChestMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(Zone1Path, OpenSceneMode.Single);
            GameObject chest = EnsurePracticeChest();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = chest;
            Debug.Log($"[LockpickPracticeChestEditor] Practice chest ready at {chest.transform.position} in Zone 1. Move it freely; re-running keeps its position.");
        }

        /// <summary>Creates or refreshes the practice chest in the open Zone 1 scene.</summary>
        public static GameObject EnsurePracticeChest()
        {
            GameObject chest = GameObject.Find(ChestName);
            if (chest == null)
            {
                chest = CreateChestBody();
                chest.transform.position = FindPlacement();
                chest.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
                Undo.RegisterCreatedObjectUndo(chest, "Add Lockpick Practice Chest");
            }

            // A lock and a free chest on one object would fight over the click
            ChestRewardInteraction plainChest = chest.GetComponent<ChestRewardInteraction>();
            if (plainChest != null) Object.DestroyImmediate(plainChest);

            // Closed lid on top, so the chest reads as locked until it is picked
            Transform lid = FindChildLid(chest.transform);
            if (lid == null) lid = CreateLid(chest.transform);

            BoxCollider box = chest.GetComponent<BoxCollider>();
            if (box == null) box = chest.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.7f, 0f);
            box.size = new Vector3(2.4f, 1.4f, 1.7f);

            LockpickInteraction lockpick = chest.GetComponent<LockpickInteraction>();
            if (lockpick == null) lockpick = chest.AddComponent<LockpickInteraction>();

            SerializedObject so = new SerializedObject(lockpick);
            so.FindProperty("promptMessage").stringValue = "Pick Lock (Practice Chest)";
            so.FindProperty("interactionRadius").floatValue = 3.0f;
            so.FindProperty("lockpickDC").intValue = 8;
            so.FindProperty("pinCount").intValue = 3;
            so.FindProperty("maxSlips").intValue = 3;
            so.FindProperty("rewardGold").intValue = 10;
            so.FindProperty("hasTrap").boolValue = false;
            so.FindProperty("trapDamage").intValue = 0;
            so.FindProperty("isLocked").boolValue = true;
            so.FindProperty("chestLid").objectReferenceValue = lid;
            so.FindProperty("openLidRotation").vector3Value = OpenLidRotation;
            so.ApplyModifiedProperties();

            return chest;
        }

        private static Transform CreateLid(Transform chest)
        {
            GameObject chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath);
            GameObject lidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LidPrefabPath);
            if (chestPrefab == null || lidPrefab == null) return null;

            // Both prefabs were saved from the same scene, so their offset is where the lid sits closed
            Transform chestRoot = chestPrefab.transform;
            Transform lidRoot = lidPrefab.transform;

            GameObject lid = (GameObject)PrefabUtility.InstantiatePrefab(lidPrefab);
            lid.name = LidName;
            lid.transform.SetParent(chest, false);
            lid.transform.localPosition = chestRoot.InverseTransformPoint(lidRoot.position);
            lid.transform.localRotation = Quaternion.Inverse(chestRoot.rotation) * lidRoot.rotation;
            lid.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(lid, "Add Practice Chest Lid");
            return lid.transform;
        }

        private static GameObject CreateChestBody()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath);
            GameObject chest;
            if (prefab != null)
            {
                chest = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chest.transform.localScale = new Vector3(1.4f, 0.9f, 1.0f);
                Object.DestroyImmediate(chest.GetComponent<Collider>());
            }

            chest.name = ChestName;
            return chest;
        }

        private static Vector3 FindPlacement()
        {
            GameObject baldur = GameObject.Find("NPC_Baldur");
            Vector3 pos = baldur != null ? baldur.transform.position + OffsetFromBaldur : FallbackPosition;

            // Sit on whatever ground is there
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 20f, pos.z), Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            {
                pos.y = hit.point.y;
            }
            else
            {
                pos.y = 0f;
            }
            return pos;
        }

        private static Transform FindChildLid(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.name.IndexOf("lid", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }

                Transform nested = FindChildLid(child);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
