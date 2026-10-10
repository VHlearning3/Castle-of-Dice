using UnityEditor;
using UnityEngine;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Puts Sir Roland's steel-grey armour texture (Assets/Characters/PlayerKnight_Steel.jpg, made by
    /// BlenderSources/Knight_SteelTexture.py from the Tripo texture) on M_PlayerKnight, with the same import
    /// settings as the original. The original texture stays in the project; pointing the material back at it undoes this.
    /// Batch: -executeMethod CastleOfTheD20.Editor.KnightArmourTextureSetup.Run
    /// </summary>
    public static class KnightArmourTextureSetup
    {
        public const string OriginalTexture = "Assets/Characters/tripo_rgb_9f4fbd7c-4c41-4556-8b04-e296ba1d0d00.jpg";
        public const string SteelTexture = "Assets/Characters/PlayerKnight_Steel.jpg";
        public const string KnightMaterial = "Assets/Characters/Materials/M_PlayerKnight.mat";

        [MenuItem("CastleOfDice/Heroes/Use Steel-Grey Knight Armour")]
        public static void Run()
        {
            AssetDatabase.ImportAsset(SteelTexture, ImportAssetOptions.ForceUpdate);
            TextureImporter original = (TextureImporter)AssetImporter.GetAtPath(OriginalTexture);
            TextureImporter steel = (TextureImporter)AssetImporter.GetAtPath(SteelTexture);
            if (original == null || steel == null) { Debug.LogError("[KnightArmour] texture missing"); return; }

            TextureImporterSettings settings = new TextureImporterSettings();
            original.ReadTextureSettings(settings);
            steel.SetTextureSettings(settings);
            steel.SetPlatformTextureSettings(original.GetDefaultPlatformTextureSettings());
            foreach (string platform in new[] { "Standalone", "WebGL" })
            {
                TextureImporterPlatformSettings p = original.GetPlatformTextureSettings(platform);
                if (p.overridden) steel.SetPlatformTextureSettings(p);
            }
            steel.SaveAndReimport();

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SteelTexture);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(KnightMaterial);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log("[KnightArmour] M_PlayerKnight uses " + SteelTexture);
        }
    }
}
