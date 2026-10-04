using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Adds the game's ending to the Throne Room scene in place: a "Throne_Ending" object with
    /// <see cref="ThroneRoomEnding"/> and a hidden Elder Othelia standing just inside the Great Hall door.
    /// Running it again only refreshes those two objects; nothing else in the scene changes.
    /// </summary>
    public static class ThroneRoomEndingSetup
    {
        public const string ScenePath = "Assets/Scenes/Zone_7_ThroneRoom.unity";
        public const string OtheliaPrefabPath = "Assets/PREFABS/NPCs/NPC_Othelia_3dmodel.prefab";
        public const string RootName = "Throne_Ending";
        public const string OtheliaName = "Ending_Othelia";

        /// <summary>Where Othelia appears: a few metres inside the hall door (z -49.5), facing the throne.</summary>
        private static readonly Vector3 OtheliaSpot = new Vector3(0f, 0f, -45.5f);

        [MenuItem("CastleOfDice/Setup Throne Room Ending", false, 13)]
        public static void SetupMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Setup();
        }

        /// <summary>Batch-mode entry: sets up the ending in Zone 7 and saves the scene.</summary>
        public static void Setup()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject root = FindRoot(scene, RootName) ?? new GameObject(RootName);
            ThroneRoomEnding ending = root.GetComponent<ThroneRoomEnding>() ?? root.AddComponent<ThroneRoomEnding>();

            Transform oldOthelia = root.transform.Find(OtheliaName);
            if (oldOthelia != null) Object.DestroyImmediate(oldOthelia.gameObject);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OtheliaPrefabPath);
            GameObject othelia = null;
            if (prefab != null)
            {
                othelia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                othelia.name = OtheliaName;
                othelia.transform.SetParent(root.transform, false);
                othelia.transform.SetPositionAndRotation(OtheliaSpot, Quaternion.identity);

                // She speaks only through the ending; clicking her must not replay the village quest
                VillageNPC npc = othelia.GetComponent<VillageNPC>();
                if (npc != null) npc.IsInteractable = false;
                othelia.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"[ThroneRoomEndingSetup] Othelia prefab not found at {OtheliaPrefabPath}.");
            }

            GameObject knights = GameObject.Find(ThroneRoomEnding.PetrifiedKnightsName);
            LavaCracksReveal cracks = Object.FindAnyObjectByType<LavaCracksReveal>();
            GameObject centerLight = GameObject.Find("CrownLight_Center");

            ending.Configure(othelia, knights != null ? knights.transform : null, cracks,
                centerLight != null ? centerLight.GetComponent<Light>() : null);
            EditorUtility.SetDirty(ending);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ThroneRoomEndingSetup] Throne Room ending set up and Zone 7 saved.");
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go;
            }
            return null;
        }
    }
}
