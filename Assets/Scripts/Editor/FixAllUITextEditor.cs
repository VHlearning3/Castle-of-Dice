using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Editor utility that iterates through all UI text areas and buttons in the active scene and UI prefabs,
    /// fixing text properties: zeroing out corrupt margins, enabling auto-sizing, enabling word wrapping,
    /// and disabling raycastTarget on text so clicks reliably reach button graphics.
    /// </summary>
    public static class FixAllUITextEditor
    {
        [MenuItem("CastleOfDice/Fix All UI Text and Buttons")]
        public static void FixAllUI()
        {
            int sceneTextCount = 0;
            int prefabTextCount = 0;

            // 1. Process all TMP_Text components in open scenes
            TMP_Text[] sceneTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text txt in sceneTexts)
            {
                if (txt == null) continue;

                Undo.RecordObject(txt, "Fix Text Properties");

                txt.margin = Vector4.zero;
                txt.enableAutoSizing = true;
                txt.textWrappingMode = TextWrappingModes.Normal;
                txt.raycastTarget = false;
                txt.overflowMode = TextOverflowModes.Overflow;

                float currentSize = txt.fontSize;
                if (currentSize >= 50f)
                {
                    txt.fontSizeMin = 20f;
                    txt.fontSizeMax = currentSize;
                }
                else if (currentSize >= 30f)
                {
                    txt.fontSizeMin = 16f;
                    txt.fontSizeMax = currentSize;
                }
                else
                {
                    txt.fontSizeMin = 12f;
                    txt.fontSizeMax = Mathf.Max(20f, currentSize);
                }

                EditorUtility.SetDirty(txt);
                sceneTextCount++;
            }

            // 2. Mark active scene dirty
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            // 3. Process OptionButtonPrefab
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/PREFABS/UI" });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                if (prefab == null) continue;

                bool modified = false;
                TMP_Text[] prefabTexts = prefab.GetComponentsInChildren<TMP_Text>(true);
                foreach (TMP_Text pt in prefabTexts)
                {
                    pt.margin = Vector4.zero;
                    pt.enableAutoSizing = true;
                    pt.textWrappingMode = TextWrappingModes.Normal;
                    pt.raycastTarget = false;
                    pt.fontSizeMin = 12f;
                    pt.fontSizeMax = Mathf.Max(24f, pt.fontSize);
                    pt.overflowMode = TextOverflowModes.Overflow;
                    modified = true;
                    prefabTextCount++;
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[FixAllUITextEditor] Successfully sanitized {sceneTextCount} scene text components and {prefabTextCount} prefab text components. All margins reset to 0, auto-sizing enabled, raycastTarget disabled.");
            EditorUtility.DisplayDialog("Fix UI Text & Buttons", $"Successfully updated:\n- {sceneTextCount} Scene Text Components\n- {prefabTextCount} Prefab Text Components\n\nAll margins zeroed, auto-sizing enabled, and raycastTarget disabled.", "OK");
        }
    }
}
