using UnityEngine;
using UnityEditor;
using TMPro;
using System.Text;

public class EmojiRemover : EditorWindow
{
    [MenuItem("Tools/Remove Emojis (From Scene and Prefabs)")]
    public static void RemoveEmojis()
    {
        int modifiedCount = 0;

        // 1. Clean currently open scene
        TMP_Text[] allTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_Text txt in allTexts)
        {
            if (CleanText(txt)) modifiedCount++;
        }

        // 2. Clean all prefabs across the project
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                bool prefabModified = false;
                TMP_Text[] prefabTexts = prefab.GetComponentsInChildren<TMP_Text>(true);

                foreach (TMP_Text txt in prefabTexts)
                {
                    if (CleanText(txt)) prefabModified = true;
                }

                if (prefabModified)
                {
                    EditorUtility.SetDirty(prefab);
                    modifiedCount++;
                }
            }
        }

        // Save all changes at once
        AssetDatabase.SaveAssets();

        Debug.Log($"<color=green>Cleanup complete!</color> Emojis and symbols removed. Modified {modifiedCount} objects/prefabs total.");
    }

    private static bool CleanText(TMP_Text txt)
    {
        if (string.IsNullOrEmpty(txt.text)) return false;

        StringBuilder sb = new StringBuilder();
        bool hasChanges = false;

        // Iterate character by character
        for (int i = 0; i < txt.text.Length; i++)
        {
            char c = txt.text[i];

            // Filter 1: Remove all surrogate pairs (e.g., emojis)
            if (char.IsSurrogate(c))
            {
                hasChanges = true;
                continue;
            }

            // Filter 2: Remove Miscellaneous Symbols and Dingbats (\u2600 - \u27BF)
            if (c >= '\u2600' && c <= '\u27BF')
            {
                hasChanges = true;
                continue;
            }

            sb.Append(c);
        }

        if (hasChanges)
        {
            Undo.RecordObject(txt, "Remove Emojis");
            txt.text = sb.ToString();

            // Ensure prefab instance modifications are recorded
            PrefabUtility.RecordPrefabInstancePropertyModifications(txt);
            return true;
        }

        return false;
    }
}