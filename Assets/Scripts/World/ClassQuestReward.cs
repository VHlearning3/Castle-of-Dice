using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Sits on the chest at the end of a hero's own quest (the knight's crates, the mage's secret room, the
    /// rogue's strongbox). Opening the chest gives that hero +1 weapon damage and a level (ClassQuests).
    /// </summary>
    [RequireComponent(typeof(ChestRewardInteraction))]
    public class ClassQuestReward : MonoBehaviour
    {
        [Tooltip("Whose quest this chest ends.")]
        [SerializeField] private CharacterClassType questClass = CharacterClassType.Warrior;

        private ChestRewardInteraction chest;

        /// <summary>Whose quest this chest ends.</summary>
        public CharacterClassType QuestClass
        {
            get => questClass;
            set => questClass = value;
        }

        private void Awake()
        {
            chest = GetComponent<ChestRewardInteraction>();
            if (chest != null) chest.OnInteracted += HandleOpened;
        }

        private void OnDestroy()
        {
            if (chest != null) chest.OnInteracted -= HandleOpened;
        }

        private void HandleOpened(PlayerUnit hero)
        {
            if (ClassQuests.GrantReward(ClassQuests.For(questClass), hero))
            {
                UI.LockpickMinigameUI.ShowToast("Weapon upgraded: +1 damage!");
                Audio.SFXManager.Instance?.PlaySFX(Audio.SFXClipType.CriticalSuccess, transform.position);
            }
        }
    }
}
