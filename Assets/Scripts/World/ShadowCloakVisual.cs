using UnityEngine;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// While the Shadow Cloak is worn the hero is wrapped in shadow: every part of the model is tinted a dusky
    /// violet (Vili, 2026-10-06). Uses property blocks only, so no material is changed; taking the cloak off
    /// clears them. Added to the hero by PlayerUnit.
    /// </summary>
    public class ShadowCloakVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>The shadow tint multiplied into the hero's colours.</summary>
        public static readonly Color Tint = new Color(0.40f, 0.34f, 0.58f, 1f);

        private const float RefreshSeconds = 2f;

        private MaterialPropertyBlock block;
        private bool applied;
        private float refreshTimer;

        /// <summary>Whether the shadow tint is on.</summary>
        public bool IsApplied => applied;

        private void Update()
        {
            bool unseen = MagicItems.IsUnseen;
            if (unseen != applied)
            {
                Apply(unseen);
                return;
            }

            // The hero's class model can be swapped: tint the new parts too
            if (unseen)
            {
                refreshTimer -= Time.unscaledDeltaTime;
                if (refreshTimer <= 0f) Apply(true);
            }
        }

        private void OnDisable()
        {
            if (applied) Apply(false);
        }

        /// <summary>Turns the shadow tint on or off.</summary>
        public void Apply(bool on)
        {
            applied = on;
            refreshTimer = RefreshSeconds;
            if (block == null) block = new MaterialPropertyBlock();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;

                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    block.Clear();
                    Material m = materials[i];
                    if (on && m != null)
                    {
                        Color from = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : (m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white);
                        Color tinted = new Color(from.r * Tint.r, from.g * Tint.g, from.b * Tint.b, from.a);
                        if (m.HasProperty(BaseColorId)) block.SetColor(BaseColorId, tinted);
                        if (m.HasProperty(ColorId)) block.SetColor(ColorId, tinted);
                    }
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
