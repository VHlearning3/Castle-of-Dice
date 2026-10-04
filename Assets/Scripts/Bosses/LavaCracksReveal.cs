using UnityEngine;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.Bosses
{
    /// <summary>
    /// Throne Room phase switch: keeps the lava cracks (and their sparks and glow) hidden until the Gargoyle King
    /// turns to stone, then shows them, turns the room's centre light and the sun to the lava palette and shakes
    /// the camera once.
    /// </summary>
    public class LavaCracksReveal : MonoBehaviour
    {
        [Tooltip("Child holding the crack meshes, sparks and glow lights. Hidden until phase 2.")]
        public GameObject cracks;

        [Header("Phase 2 lights")]
        public Light centerLight;
        public Color centerPhase2Color = new Color(1f, 0.35f, 0.1f);
        public float centerPhase2Intensity = 3f;
        public Light sun;
        public Color sunPhase2Color = new Color(1f, 0.4f, 0.2f);
        public float sunPhase2Intensity = 1.3f;

        private Color centerStartColor;
        private float centerStartIntensity;
        private Color sunStartColor;
        private float sunStartIntensity;

        private void Awake()
        {
            if (cracks != null) cracks.SetActive(false);
            if (centerLight != null)
            {
                centerStartColor = centerLight.color;
                centerStartIntensity = centerLight.intensity;
            }
            if (sun != null)
            {
                sunStartColor = sun.color;
                sunStartIntensity = sun.intensity;
            }
        }

        private void OnEnable()
        {
            GargoyleKingBoss.OnStoneFormActivated += HandleStoneForm;
            GargoyleKingBoss.OnStoneFormReset += HandleStoneFormReset;
        }

        private void OnDisable()
        {
            GargoyleKingBoss.OnStoneFormActivated -= HandleStoneForm;
            GargoyleKingBoss.OnStoneFormReset -= HandleStoneFormReset;
        }

        private void HandleStoneFormReset(GargoyleKingBoss boss) => Restore();

        /// <summary>Closes the cracks and gives the room its first-phase lights back (a retry, or the curse breaking).</summary>
        public void Restore()
        {
            if (cracks != null) cracks.SetActive(false);
            if (centerLight != null)
            {
                centerLight.color = centerStartColor;
                centerLight.intensity = centerStartIntensity;
            }
            if (sun != null)
            {
                sun.color = sunStartColor;
                sun.intensity = sunStartIntensity;
            }
        }

        private void HandleStoneForm(GargoyleKingBoss boss)
        {
            if (cracks != null) cracks.SetActive(true);
            if (centerLight != null)
            {
                centerLight.color = centerPhase2Color;
                centerLight.intensity = centerPhase2Intensity;
            }
            if (sun != null)
            {
                sun.color = sunPhase2Color;
                sun.intensity = sunPhase2Intensity;
            }
            AbilityVfx.PlayCameraShake(0.45f, 0.2f);
        }
    }
}
