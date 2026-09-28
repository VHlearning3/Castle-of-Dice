using System;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Audio;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Ancient Runestone Shrine situated in the Castle Central Hall (Safe Haven Hub).
    /// Interacting with the Shrine restores hero health to 100% and triggers persistent game saving
    /// via SaveSystem.SaveGame() into PlayerPrefs JSON (zero file-system IO for WebGL air-gap).
    /// </summary>
    [SelectionBase]
    public class SavePoint : Interactable
    {
        #region Serialized Fields

        [Header("Shrine Identity")]
        [Tooltip("Display name of this sacred resting place.")]
        [SerializeField] private string shrineName = "Ancient Runestone Shrine";

        [Header("Restoration & Visuals")]
        [Tooltip("Particle system pulsed upon communion with the shrine.")]
        [SerializeField] private ParticleSystem communeParticles;

        [Tooltip("Audio clip played upon communing and saving.")]
        [SerializeField] private AudioClip communeSound;

        [Header("Feedback Messages")]
        [Tooltip("Notification message displayed upon successful healing and saving.")]
        [SerializeField] private string successMessage = "Health restored to 100% and adventure progress saved!";

        #endregion

        #region Events

        /// <summary>Fired when player rests at and saves at a save shrine.</summary>
        public static event Action<SavePoint, PlayerUnit> OnGameSavedAtShrine;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            promptMessage = "Commune with Runestone (Restore HP & Save)";
            interactionRadius = 3.0f;
        }

        #endregion

        #region Interactable Overrides

        public override void Interact(PlayerUnit player)
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerUnit>();
            }

            if (player == null)
            {
                Debug.LogWarning("[SavePoint] Cannot commune with shrine: no PlayerUnit found.");
                return;
            }

            CommuneAndSave(player);
        }

        #endregion

        #region Shrine Logic

        /// <summary>
        /// Restores player health to maximum, triggers SaveSystem.SaveGame(), and provides feedback.
        /// </summary>
        public void CommuneAndSave(PlayerUnit player)
        {
            if (player == null) return;

            // 1. Fully restore hero HP to 100%
            int healedAmount = player.MaxHP - player.CurrentHP;
            player.Heal(player.MaxHP);
            Debug.Log($"<color=#38bdf8><b>[SavePoint] {player.UnitName} communes with {shrineName}! Healed {healedAmount} HP to full (HP: {player.MaxHP}/{player.MaxHP}).</b></color>");

            // 2. Persist progression, economy, and stats via SaveSystem
            PlayerDataSO dataSO = null;
            if (PlayerProgressionManager.Instance != null)
            {
                dataSO = PlayerProgressionManager.Instance.PlayerData;
                if (dataSO != null)
                {
                    dataSO.SyncFromPlayer(player);
                }
            }

            SaveSystem.SaveGame(dataSO, player);

            // 3. Audio & Particle Feedback
            if (communeParticles != null)
            {
                communeParticles.Play();
            }

            if (communeSound != null)
            {
                if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(communeSound, transform.position);
            }
            else if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlaySFX(SFXClipType.SpellCast, transform.position);
            }

            OnGameSavedAtShrine?.Invoke(this, player);
            Debug.Log($"<color=#00e676><b>[SavePoint] {successMessage}</b></color>");
        }

        #endregion
    }
}
