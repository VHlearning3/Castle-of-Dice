using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.Data
{
    /// <summary>
    /// Persistent ScriptableObject holding hero milestone progression, attribute improvements,
    /// and upgraded abilities across scenes.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerData", menuName = "CastleOfDice/Data/PlayerData", order = 15)]
    public class PlayerDataSO : ScriptableObject
    {
        #region Progression Data

        [Header("Milestone Progression")]
        [Range(1, 3)]
        [SerializeField] private int currentLevel = 1;

        [Header("Permanent Stat Bonuses")]
        [SerializeField] private int maxHPBonus = 0;
        [SerializeField] private int attributeBonusModifier = 0;
        [SerializeField] private int permanentWeaponDamageBonus = 0;
        [SerializeField] private int permanentArmorClassBonus = 0;

        [Header("Ability Upgrades")]
        [SerializeField] private List<int> upgradedAbilityIndices = new List<int>();

        [Header("Currency & Resources")]
        [SerializeField] private int gold = 0;
        [SerializeField] private int scrapMetal = 0;

        #endregion

        #region Public Properties

        public int CurrentLevel
        {
            get => currentLevel;
            set => currentLevel = Mathf.Clamp(value, 1, 3);
        }

        public int MaxHPBonus
        {
            get => maxHPBonus;
            set => maxHPBonus = Mathf.Max(0, value);
        }

        public int AttributeBonusModifier
        {
            get => attributeBonusModifier;
            set => attributeBonusModifier = Mathf.Max(0, value);
        }

        public int WeaponDamageBonus
        {
            get => permanentWeaponDamageBonus;
            set => permanentWeaponDamageBonus = Mathf.Max(0, value);
        }

        public int ArmorClassBonus
        {
            get => permanentArmorClassBonus;
            set => permanentArmorClassBonus = Mathf.Max(0, value);
        }

        public IReadOnlyList<int> UpgradedAbilityIndices => upgradedAbilityIndices;

        public int Gold
        {
            get => gold;
            set => gold = Mathf.Max(0, value);
        }

        public int ScrapMetal
        {
            get => scrapMetal;
            set => scrapMetal = Mathf.Max(0, value);
        }

        #endregion

        #region Management Methods

        /// <summary>
        /// Resets progression to default Level 1 state.
        /// </summary>
        public void ResetData()
        {
            currentLevel = 1;
            maxHPBonus = 0;
            attributeBonusModifier = 0;
            permanentWeaponDamageBonus = 0;
            permanentArmorClassBonus = 0;
            upgradedAbilityIndices.Clear();
            gold = 0;
            scrapMetal = 0;
        }

        /// <summary>
        /// Records an ability index as upgraded to Rank 2.
        /// </summary>
        public void RegisterUpgradedAbility(int slotIndex)
        {
            if (!upgradedAbilityIndices.Contains(slotIndex))
            {
                upgradedAbilityIndices.Add(slotIndex);
            }
        }

        /// <summary>
        /// Applies saved progression stats to a live PlayerUnit instance.
        /// </summary>
        public void ApplyToPlayer(PlayerUnit player)
        {
            if (player == null) return;

            player.Level = currentLevel;

            if (maxHPBonus > 0)
            {
                player.ApplyHeroResilience(maxHPBonus);
            }

            if (attributeBonusModifier > 0)
            {
                player.AddAttributeBonus(attributeBonusModifier);
            }

            if (permanentWeaponDamageBonus > 0)
            {
                player.AddWeaponDamageBonus(permanentWeaponDamageBonus);
            }

            if (permanentArmorClassBonus > 0)
            {
                player.AddArmorClassBonus(permanentArmorClassBonus);
            }

            foreach (int slot in upgradedAbilityIndices)
            {
                player.UpgradeAbilityToRank2(slot);
            }
        }

        /// <summary>
        /// Synchronizes data from active PlayerUnit into this ScriptableObject.
        /// </summary>
        public void SyncFromPlayer(PlayerUnit player)
        {
            if (player == null) return;

            currentLevel = player.Level;
            permanentWeaponDamageBonus = player.WeaponDamageBonus;
            permanentArmorClassBonus = player.ArmorClassBonus;
        }

        #endregion
    }
}
