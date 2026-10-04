using UnityEditor;
using UnityEngine;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Critical review D4: the low-poly pack's textures are 2048 px with mipmaps (about 5 MB each in the
    /// WebGL build), which makes the browser download over 200 MB. This caps them at 1024 px for the WebGL
    /// build only; the editor and desktop builds keep the full size. Nothing is deleted.
    /// Batch: -executeMethod CastleOfTheD20.Editor.WebGLTextureCap.Apply
    /// </summary>
    public static class WebGLTextureCap
    {
        public const int MaxWebGLSize = 1024;
        private static readonly string[] Folders = { "Assets/LowPolyVillageAll" };

        [MenuItem("CastleOfDice/Tools/Cap Pack Textures for WebGL (1024)", false, 61)]
        public static void Apply()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", Folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                TextureImporterPlatformSettings webgl = importer.GetPlatformTextureSettings("WebGL");
                if (webgl.overridden && webgl.maxTextureSize <= MaxWebGLSize) continue;

                TextureImporterPlatformSettings defaults = importer.GetDefaultPlatformTextureSettings();
                webgl.overridden = true;
                webgl.maxTextureSize = Mathf.Min(MaxWebGLSize, defaults.maxTextureSize);
                webgl.format = TextureImporterFormat.Automatic;
                webgl.textureCompression = defaults.textureCompression;
                webgl.compressionQuality = defaults.compressionQuality;
                webgl.crunchedCompression = defaults.crunchedCompression;
                importer.SetPlatformTextureSettings(webgl);
                importer.SaveAndReimport();
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[WebGLTextureCap] Capped {changed} pack textures at {MaxWebGLSize} px for WebGL.");
        }

        /// <summary>Applies the cap, then builds WebGL and writes the size report.</summary>
        public static void ApplyAndReport()
        {
            Apply();
            WebGLSizeReport.Run();
        }
    }
}
