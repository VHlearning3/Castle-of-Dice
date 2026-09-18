using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Data
{
    /// <summary>
    /// ScriptableObject defining a playable hero class archetype (Warrior, Mage, Rogue).
    /// Stores core attributes, baseline statistics, visual representations (portrait and 3D prefab),
    /// and the fixed set of 4 starting combat abilities.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacterClass", menuName = "CastleOfDice/Data/Character Class", order = 11)]
    public class CharacterClassSO : ScriptableObject
    {
        #region Serialized Fields

        [Header("Class Identity")]
        [Tooltip("Core class archetype: Warrior (Sir Roland), Mage (Elira), or Rogue (Shadow-Corvo).")]
        [SerializeField] private CharacterClassType classType = CharacterClassType.Warrior;

        [Tooltip("Canonical name of the character (e.g. 'Sir Roland', 'Scholar Elira', 'Shadow-Corvo').")]
        [SerializeField] private string characterName = "Hero";

        [Tooltip("Narrative lore, history, and motivations described in character selection.")]
        [TextArea(3, 6)]
        [SerializeField] private string backgroundLore = "Character background story and role in the expedition.";

        [Header("Base Combat Statistics")]
        [Tooltip("Maximum health points at the beginning of an encounter.")]
        [SerializeField] private int baseMaxHealth = 30;

        [Tooltip("Baseline Armor Class (AC) determining the Difficulty Class enemies must meet to hit.")]
        [SerializeField] private int baseArmorClass = 14;

        [Tooltip("Maximum movement distance across grid tiles allowed per combat turn.")]
        [Range(1, 10)]
        [SerializeField] private int baseMovementRange = 3;

        [Tooltip("Primary attribute bonus (+2 to +5) added to D20 attack and skill check rolls.")]
        [Range(1, 10)]
        [SerializeField] private int primaryAttributeBonus = 3;

        [Header("Starting Abilities (Exactly 4)")]
        [Tooltip("Fixed loadout of 4 class-specific active abilities available during combat.")]
        [SerializeField] private List<AbilitySO> startingAbilities = new List<AbilitySO>(4);

        [Header("Visual Assets")]
        [Tooltip("2D portrait sprite displayed in dialogue boxes, turn trackers, and character HUD.")]
        [SerializeField] private Sprite characterPortrait;

        [Tooltip("3D character model prefab instantiated on the combat and village grid.")]
        [SerializeField] private GameObject characterPrefab;

        #endregion

        #region Public Properties

        /// <summary>The archetype classification of this character.</summary>
        public CharacterClassType ClassType => classType;

        /// <summary>Hero display name.</summary>
        public string CharacterName => characterName;

        /// <summary>Background narrative story.</summary>
        public string BackgroundLore => backgroundLore;

        /// <summary>Starting maximum hit points.</summary>
        public int BaseMaxHealth => baseMaxHealth;

        /// <summary>Starting Armor Class.</summary>
        public int BaseArmorClass => baseArmorClass;

        /// <summary>Movement range in grid tiles per turn.</summary>
        public int BaseMovementRange => baseMovementRange;

        /// <summary>Primary attribute modifier added to D20 checks (+2 to +5).</summary>
        public int PrimaryAttributeBonus => primaryAttributeBonus;

        /// <summary>Read-only access to the 4 starting abilities.</summary>
        public IReadOnlyList<AbilitySO> StartingAbilities => startingAbilities;

        /// <summary>Character portrait sprite.</summary>
        public Sprite CharacterPortrait => characterPortrait;

        /// <summary>Instantiable 3D character prefab.</summary>
        public GameObject CharacterPrefab => characterPrefab;

        #endregion

        #region Validation & Integrity

        private void OnValidate()
        {
            if (baseMaxHealth < 1) baseMaxHealth = 1;
            if (baseArmorClass < 1) baseArmorClass = 1;
            if (baseMovementRange < 1) baseMovementRange = 1;
            if (primaryAttributeBonus < 0) primaryAttributeBonus = 0;

            if (startingAbilities != null && startingAbilities.Count > 4)
            {
                Debug.LogWarning($"[CharacterClassSO] {characterName} ({classType}) has more than 4 abilities assigned. Castle of the D20 standard loadout is exactly 4.");
            }
        }

        #endregion
    }
}
