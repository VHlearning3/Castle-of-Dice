using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Data
{
    /// <summary>
    /// Configuration ScriptableObject mapping game zones and encounters to discrete background music tracks.
    /// Used by MusicManager for smooth equal-power crossfading across locations and combat states.
    /// </summary>
    [CreateAssetMenu(fileName = "ZoneMusicConfig", menuName = "CastleOfDice/Audio/ZoneMusicConfig", order = 20)]
    public class ZoneMusicSO : ScriptableObject
    {
        #region Serialized Fields

        [Header("Zone Exploration Music Mappings")]
        [Tooltip("Background theme for Oakhaven Village.")]
        [SerializeField] private MusicTrackType villageTrack = MusicTrackType.Village;

        [Tooltip("Background theme for Whispering Woods (Forest Path).")]
        [SerializeField] private MusicTrackType forestTrack = MusicTrackType.CastleAdventure;

        [Tooltip("Background theme for Castle Courtyard exploration.")]
        [SerializeField] private MusicTrackType courtyardTrack = MusicTrackType.CastleAdventure;

        [Tooltip("Background theme for Grand Archives (Library) exploration.")]
        [SerializeField] private MusicTrackType libraryTrack = MusicTrackType.CastleAdventure;

        [Tooltip("Background theme for Castle Central Hall (Safe Haven Hub).")]
        [SerializeField] private MusicTrackType castleHallTrack = MusicTrackType.CastleAdventure;

        [Tooltip("Background theme for Hidden Treasure Tower.")]
        [SerializeField] private MusicTrackType towerTrack = MusicTrackType.CastleAdventure;

        [Tooltip("Background theme for Crown Hall (Throne Room) exploration.")]
        [SerializeField] private MusicTrackType crownHallTrack = MusicTrackType.CastleAdventure;

        [Header("Encounter & Boss Combat Music Mappings")]
        [Tooltip("Combat theme for Tavern Cellar rats encounter.")]
        [SerializeField] private MusicTrackType cellarCombatTrack = MusicTrackType.CellarCombat;

        [Tooltip("Combat theme for Wing 1 Boss (Cursed Commander).")]
        [SerializeField] private MusicTrackType cursedCommanderTrack = MusicTrackType.CursedCommanderCombat;

        [Tooltip("Combat theme for Wing 2 Boss (Shadow Mage Malakor).")]
        [SerializeField] private MusicTrackType malakorTrack = MusicTrackType.MalakorCombat;

        [Tooltip("Combat theme for Wing 3 Final Boss (Gargoyle King Phase 1).")]
        [SerializeField] private MusicTrackType gargoyleKingPhase1Track = MusicTrackType.GargoyleKingPhase1;

        [Tooltip("Combat theme for Wing 3 Final Boss (Gargoyle King Phase 2 - Stone Form).")]
        [SerializeField] private MusicTrackType gargoyleKingPhase2Track = MusicTrackType.GargoyleKingPhase2;

        #endregion

        #region Public Properties

        public MusicTrackType GargoyleKingPhase1Track => gargoyleKingPhase1Track;
        public MusicTrackType GargoyleKingPhase2Track => gargoyleKingPhase2Track;

        #endregion

        #region Lookup Helpers

        /// <summary>
        /// Retrieves the configured exploration music track for a given GameLocation.
        /// </summary>
        public MusicTrackType GetTrackForLocation(GameLocation location)
        {
            switch (location)
            {
                case GameLocation.Village:
                    return villageTrack;
                case GameLocation.Forest:
                    return forestTrack;
                case GameLocation.Courtyard:
                    return courtyardTrack;
                case GameLocation.Library:
                    return libraryTrack;
                case GameLocation.CastleHall:
                    return castleHallTrack;
                case GameLocation.Tower:
                    return towerTrack;
                case GameLocation.CrownHall:
                    return crownHallTrack;
                case GameLocation.Cellar:
                    return forestTrack;
                default:
                    return castleHallTrack;
            }
        }

        /// <summary>
        /// Resolves the target combat track based on boss identifier and room location.
        /// </summary>
        public MusicTrackType GetCombatTrack(string bossIdentifier, string roomLocation = "")
        {
            string boss = (bossIdentifier ?? string.Empty).Trim();
            string room = (roomLocation ?? string.Empty).Trim();

            if (boss.IndexOf("Commander", StringComparison.OrdinalIgnoreCase) >= 0 ||
                boss.IndexOf("Komentaja", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return cursedCommanderTrack;
            }
            if (boss.IndexOf("Malakor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                boss.IndexOf("Varjomaagi", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return malakorTrack;
            }
            if (boss.IndexOf("Gargoyle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                boss.IndexOf("Kivettymiskuningas", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return gargoyleKingPhase1Track;
            }
            if (room.IndexOf("Cellar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                room.IndexOf("Kellari", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return cellarCombatTrack;
            }

            return cellarCombatTrack;
        }

        #endregion
    }
}
