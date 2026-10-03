using UnityEngine;

namespace CastleOfTheD20.Bosses
{
    /// <summary>
    /// Throne Room dressing: keeps the glowing lava cracks in the floor hidden until the Gargoyle King
    /// turns to stone (phase 2), then shows them for the rest of the fight.
    /// </summary>
    public class LavaCracksReveal : MonoBehaviour
    {
        [Tooltip("Child holding the crack meshes and their glow lights. Hidden until phase 2.")]
        public GameObject cracks;

        private void Awake()
        {
            if (cracks != null) cracks.SetActive(false);
        }

        private void OnEnable() => GargoyleKingBoss.OnStoneFormActivated += HandleStoneForm;
        private void OnDisable() => GargoyleKingBoss.OnStoneFormActivated -= HandleStoneForm;

        private void HandleStoneForm(GargoyleKingBoss boss)
        {
            if (cracks != null) cracks.SetActive(true);
        }
    }
}
