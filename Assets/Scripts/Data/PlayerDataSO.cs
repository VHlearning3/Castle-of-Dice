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
        #region Session Instance

        private static PlayerDataSO s_session;

        /// <summary>
        /// The progression store shared by every scene in this play session. Uses Resources/PlayerData
        /// when such an asset exists, otherwise a runtime-only instance (never written back to disk).
        /// Every zone scene spawns its own hero prefab, so this is what carries the chosen class and
        /// upgrades across scene loads.
        /// </summary>
        public static PlayerDataSO Session
        {
            get
            {
                if (s_session == null)
                {
                    s_session = Resources.Load<PlayerDataSO>("PlayerData");
                    if (s_session == null)
                    {
                        s_session = CreateInstance<PlayerDataSO>();
                        s_session.name = "PlayerData (Session)";
                        s_session.hideFlags = HideFlags.DontSave;
                    }
                }
                return s_session;
            }
            set => s_session = value;
        }

        #endregion

        #region Progression Data

        [Header("Hero")]
        [Tooltip("Class chosen in the main menu; applied to the hero prefab in every zone scene.")]
        [SerializeField] private CharacterClassSO selectedClass;

        [Header("Milestone Progression")]
        [Range(1, 5)]
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

        [Header("Health")]
        [Tooltip("Hero HP carried between zones and saves (-1 = full health).")]
        [SerializeField] private int currentHP = -1;

        #endregion

        #region Public Properties

        /// <summary>Hero class chosen for this adventure (null = keep the scene prefab's class).</summary>
        public CharacterClassSO SelectedClass
        {
            get => selectedClass;
            set => selectedClass = value;
        }

        public int CurrentLevel
        {
            get => currentLevel;
            set => currentLevel = Mathf.Clamp(value, 1, PlayerUnit.MaxLevel);
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

        /// <summary>Hero HP carried between zones and saves (-1 or 0 = full health).</summary>
        public int CurrentHP
        {
            get => currentHP;
            set => currentHP = value > 0 ? value : -1;
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
            currentHP = -1;
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

            // Absolute (base + bonus) application: safe to call on every scene load and after loading a save.
            // Read the carried HP first: applying progression reports the hero's health back to this store.
            int carriedHP = currentHP;
            player.ApplyProgression(currentLevel, maxHPBonus, attributeBonusModifier,
                permanentWeaponDamageBonus, permanentArmorClassBonus, upgradedAbilityIndices);
            if (carriedHP > 0)
            {
                player.SetCurrentHP(carriedHP);
            }
        }

        /// <summary>
        /// Synchronizes all progression data from the active PlayerUnit into this ScriptableObject.
        /// </summary>
        public void SyncFromPlayer(PlayerUnit player)
        {
            if (player == null) return;

            currentLevel = player.Level;
            maxHPBonus = player.MaxHPBonus;
            attributeBonusModifier = player.AttributeBonusModifier;
            permanentWeaponDamageBonus = player.WeaponDamageBonus;
            permanentArmorClassBonus = player.ArmorClassBonus;
            player.GetUpgradedAbilitySlots(upgradedAbilityIndices);
            CurrentHP = player.IsAlive ? player.CurrentHP : -1;
        }

        #endregion
    }
}
