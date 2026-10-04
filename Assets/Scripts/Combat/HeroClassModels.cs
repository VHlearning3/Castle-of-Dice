using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.World;
using UnityEngine;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Holds one rigged model per hero class under the hero's Visuals and shows the one matching the
    /// chosen class (Sir Roland, Elira or Corvo). The shown model's Animator becomes the unit's animator.
    /// </summary>
    public class HeroClassModels : MonoBehaviour
    {
        [SerializeField] private GameObject warriorModel;
        [SerializeField] private GameObject mageModel;
        [SerializeField] private GameObject rogueModel;

        /// <summary>Activates the model for <paramref name="classType"/> and rebinds the hero's animator to it.</summary>
        public void Apply(CharacterClassType classType)
        {
            GameObject chosen = ModelFor(classType);
            if (chosen == null) chosen = warriorModel;
            if (chosen == null) return;

            SetActive(warriorModel, warriorModel == chosen);
            SetActive(mageModel, mageModel == chosen);
            SetActive(rogueModel, rogueModel == chosen);

            // Elira's staff and Corvo's daggers hang on their hand bones (the knight's sword and shield live in the prefab)
            HeroWeaponMountsSO weapons = HeroWeaponMountsSO.Instance;
            if (weapons != null) weapons.AttachTo(chosen, chosen == warriorModel ? CharacterClassType.Warrior : classType);

            Animator animator = chosen.GetComponentInChildren<Animator>(true);
            if (animator == null) return;

            CombatUnit unit = GetComponent<CombatUnit>();
            if (unit != null) unit.UnitAnimator = animator;

            PlayerExplorationMovement movement = GetComponent<PlayerExplorationMovement>();
            if (movement != null) movement.SetAnimator(animator);
        }

        private GameObject ModelFor(CharacterClassType classType)
        {
            switch (classType)
            {
                case CharacterClassType.Mage: return mageModel;
                case CharacterClassType.Rogue: return rogueModel;
                default: return warriorModel;
            }
        }

        private static void SetActive(GameObject model, bool active)
        {
            if (model != null && model.activeSelf != active) model.SetActive(active);
        }
    }
}
