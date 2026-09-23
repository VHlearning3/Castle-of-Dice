using UnityEngine;
using UnityEditor;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    [CustomEditor(typeof(StartSpawnPoint))]
    public class StartSpawnEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            StartSpawnPoint spawnPoint = (StartSpawnPoint)target;
            if (spawnPoint == null) return;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Editor Actions", EditorStyles.boldLabel);

            // Button: Snap Player to Spawn Position
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
            if (GUILayout.Button("Snap Player to Spawn Position", GUILayout.Height(32)))
            {
                PlayerExplorationMovement player = Object.FindAnyObjectByType<PlayerExplorationMovement>();
                if (player != null)
                {
                    Undo.RecordObject(player.transform, "Snap Player to StartSpawn");
                    CharacterController cc = player.GetComponent<CharacterController>();
                    Vector3 spawnPos = spawnPoint.GetPlayerSpawnPosition(cc);
                    player.transform.position = spawnPos;
                    player.transform.rotation = spawnPoint.transform.rotation;
                    EditorUtility.SetDirty(player.gameObject);
                    Debug.Log($"[StartSpawnEditor] Snapped Player to ({spawnPos.x:F2}, {spawnPos.y:F2}, {spawnPos.z:F2}) facing {spawnPoint.transform.eulerAngles.y:F0}°.");
                }
                else
                {
                    Debug.LogWarning("[StartSpawnEditor] No PlayerExplorationMovement found in scene.");
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.BeginHorizontal();

            // Button: Select Player in Hierarchy
            if (GUILayout.Button("Select Player in Hierarchy", GUILayout.Height(24)))
            {
                PlayerExplorationMovement player = Object.FindAnyObjectByType<PlayerExplorationMovement>();
                if (player != null)
                {
                    Selection.activeGameObject = player.gameObject;
                    EditorGUIUtility.PingObject(player.gameObject);
                }
            }

            // Button: Align Spawn to Player
            if (GUILayout.Button("Align Spawn to Player", GUILayout.Height(24)))
            {
                PlayerExplorationMovement player = Object.FindAnyObjectByType<PlayerExplorationMovement>();
                if (player != null)
                {
                    Undo.RecordObject(spawnPoint.transform, "Align StartSpawn to Player");
                    Vector3 playerPos = player.transform.position;
                    // Lower to ground level based on cube scale
                    playerPos.y -= (spawnPoint.transform.localScale.y * 0.5f + 1.0f);
                    spawnPoint.transform.position = playerPos;
                    spawnPoint.transform.rotation = player.transform.rotation;
                    EditorUtility.SetDirty(spawnPoint.gameObject);
                    Debug.Log($"[StartSpawnEditor] Aligned StartSpawn to Player position.");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Drag and rotate this StartSpawn cube anywhere in the scene to choose where the game begins.\n" +
                "During Play mode, its mesh and collider are automatically hidden so it won't block movement.",
                MessageType.Info
            );
        }
    }
}
