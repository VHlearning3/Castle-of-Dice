using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public class CodeExporter : MonoBehaviour
{
    [MenuItem("Tools/Export NotebookLM Context")]
    public static void ExportAllScripts()
    {
        string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript");
        StringBuilder stringBuilder = new StringBuilder();
        int exportedCount = 0;

        foreach (string guid in scriptGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // Skip Unity packages completely
            if (assetPath.StartsWith("Packages/"))
            {
                continue;
            }

            // Skip any script located in an Editor folder (case-insensitive check for common naming)
            if (assetPath.Contains("/Editor/") || assetPath.Contains("/editor/"))
            {
                continue;
            }

            string fileName = Path.GetFileName(assetPath);
            string fileContent = File.ReadAllText(assetPath);

            stringBuilder.AppendLine($"// ==========================================");
            stringBuilder.AppendLine($"// File: {fileName}");
            stringBuilder.AppendLine($"// Path: {assetPath}");
            stringBuilder.AppendLine($"// ==========================================");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(fileContent);
            stringBuilder.AppendLine();

            exportedCount++;
        }

        string exportPath = Path.Combine(Application.dataPath, "CombinedProjectScripts.txt");
        File.WriteAllText(exportPath, stringBuilder.ToString(), Encoding.UTF8);

        Debug.Log($"Successfully exported {exportedCount} scripts to: {exportPath}");
        AssetDatabase.Refresh();
    }
}