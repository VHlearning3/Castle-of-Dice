using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Exports all runtime scripts into a single text file for NotebookLM context.
    /// Writes to the project root (outside Assets/) so the export is not imported as a TextAsset.
    /// </summary>
    public static class CodeExporter
    {
        private const string ExportFileName = "CombinedProjectScripts.txt";

        [MenuItem("CastleOfDice/Export NotebookLM Context")]
        public static void ExportAllScripts()
        {
            string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
            StringBuilder stringBuilder = new StringBuilder();
            int exportedCount = 0;

            foreach (string guid in scriptGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // Skip editor-only scripts, tests and Unity's recovery backups
                if (assetPath.Contains("/Editor/") || assetPath.Contains("/editor/") ||
                    assetPath.StartsWith("Assets/Tests/") || assetPath.StartsWith("Assets/_Recovery/"))
                {
                    continue;
                }

                string fileName = Path.GetFileName(assetPath);
                string fileContent = File.ReadAllText(assetPath);

                stringBuilder.AppendLine("// ==========================================");
                stringBuilder.AppendLine($"// File: {fileName}");
                stringBuilder.AppendLine($"// Path: {assetPath}");
                stringBuilder.AppendLine("// ==========================================");
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(fileContent);
                stringBuilder.AppendLine();

                exportedCount++;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string exportPath = Path.Combine(projectRoot, ExportFileName);
            File.WriteAllText(exportPath, stringBuilder.ToString(), Encoding.UTF8);

            Debug.Log($"[CodeExporter] Exported {exportedCount} scripts to: {exportPath}");
        }
    }
}
