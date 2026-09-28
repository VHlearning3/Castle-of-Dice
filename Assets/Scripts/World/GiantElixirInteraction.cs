using System;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Audio;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Interactive Legendary Giant's Elixir situated in the Hidden Treasure Tower (Zone 6).
    /// Upon interaction, grants the hero a permanent +30 Max HP increase and fully heals the hero.
    /// Updates PlayerDataSO and SaveSystem so the enhancement persists across all scenes and sessions.
    /// </summary>
    [SelectionBase]
    public class GiantElixirInteraction : Interactable
    {
        #region Serialized Fields

        [Header("Elixir Properties")]
        [Tooltip("Permanent maximum health granted upon consuming this elixir.")]
        [SerializeField] private int maxHPBonus = 30;

        [Tooltip("Whether this elixir has already been consumed.")]
        [SerializeField] private bool isConsumed = false;

        [Header("Visual & Audio Feedback")]
        [Tooltip("Optional 3D visual object of the elixir bottle disabled once consumed.")]
        [SerializeField] private GameObject visualBottle;

        [Tooltip("Audio clip played upon consuming the elixir.")]
        [SerializeField] private AudioClip drinkSound;

        #endregion

        #region Public Properties

        public int MaxHPBonus => maxHPBonus;
        public bool IsConsumed => isConsumed;

        #endregion

        #region Events

        /// <summary>Fired when the player drinks the Giant Elixir: (elixir, player, bonusGranted).</summary>
        public static event Action<GiantElixirInteraction, PlayerUnit, int> OnGiantElixirConsumed;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            promptMessage = "Drink Giant's Elixir (+30 Max HP Permanently)";
            interactionRadius = 2.5f;
        }

        #endregion

        #region Interactable Overrides

        public override void Interact(PlayerUnit player)
        {
            if (isConsumed)
            {
                Debug.Log("[GiantElixirInteraction] The vial is empty; its titan essence has already been consumed.");
                return;
            }

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerUnit>();
            }

            if (player == null)
            {
                Debug.LogWarning("[GiantElixirInteraction] Cannot consume elixir without a PlayerUnit.");
                return;
            }

            ConsumeElixir(player);
        }

        #endregion

        #region Consumption Logic

        public void ConsumeElixir(PlayerUnit player)
        {
            if (isConsumed || player == null) return;

            isConsumed = true;
            promptMessage = "Empty Vial";

            // 1. Permanently increase PlayerUnit Max HP and heal fully
            player.ApplyHeroResilience(maxHPBonus);

            // 2. Persist to PlayerDataSO
            if (PlayerProgressionManager.Instance != null && PlayerProgressionManager.Instance.PlayerData != null)
            {
                PlayerProgressionManager.Instance.PlayerData.MaxHPBonus += maxHPBonus;
            }

            // 3. Audio & Visuals
            if (visualBottle != null)
            {
                visualBottle.SetActive(false);
            }

            if (drinkSound != null)
            {
                if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(drinkSound, transform.position);
            }
            else if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlaySFX(SFXClipType.PotionDrink, transform.position);
            }

            OnGiantElixirConsumed?.Invoke(this, player, maxHPBonus);
            Debug.Log($"<color=#38bdf8><b>[GiantElixirInteraction] TITAN VIGOR! {player.UnitName} consumed the Legendary Giant's Elixir (+{maxHPBonus} Max HP permanently). New Max HP: {player.MaxHP}.</b></color>");
        }

        #endregion
    }
}
