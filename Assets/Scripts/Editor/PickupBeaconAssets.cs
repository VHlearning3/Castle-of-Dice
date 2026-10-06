using System.IO;
using UnityEngine;
using UnityEditor;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Creates Resources/VFX/M_PickupBeacon: an additive URP particle material that PickupBeacon (pickup glows)
    /// and ZoneExitSign (brazier flames) copy at runtime. Keeping it in Resources keeps the shader and its
    /// additive variant in WebGL builds; without it the effects fall back to Sprites/Default (alpha blended).
    /// </summary>
    public static class PickupBeaconAssets
    {
        public const string MaterialPath = "Assets/Resources/VFX/M_PickupBeacon.mat";

        [MenuItem("CastleOfDice/Create Pickup Glow Material", false, 15)]
        public static void CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[PickupBeaconAssets] URP Particles/Unlit shader not found.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            AssetDatabase.Refresh();
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.shader = shader;

            // Transparent, additive, no depth write: a glow that brightens whatever is behind it
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.SetColor("_BaseColor", Color.white);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PickupBeaconAssets] {MaterialPath} ready.");
        }

        /// <summary>Batch entry: the glow material, then the bigger Forest Path gate (saves Zone 2).</summary>
        public static void BatchCreateAndSetupGate()
        {
            CreateMaterial();
            ForestGateSetup.BatchSetupAndScreenshots();
        }
    }
}
