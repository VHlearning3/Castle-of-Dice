using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The Treasure Tower's challenge (critical review C5). The Giant's Elixir and Othelia's ring sit sealed on
    /// their pedestal until the hero has set the three rune pillars to SUN, MOON, STAR. Pressure plates on the
    /// floor spring traps (DEX DC 12). Finishing the challenge gives a level (B8). The mimic by the door is a
    /// separate, optional fight (Vili 2026-10-10): beating it is remembered but unseals nothing.
    /// Progress is kept in story flags, so it survives saves and zone changes.
    /// </summary>
    public class TowerChallenge : MonoBehaviour
    {
        public const string RunesSolvedFlag = "TowerRunesSolved";
        public const string GuardianDefeatedFlag = "TowerGuardianDefeated";
        public const string CompletedFlag = "TowerChallengeComplete";

        /// <summary>The order the pillars must show, left to right as the hero faces the vial.</summary>
        public static readonly RunePillar.Rune[] Solution = { RunePillar.Rune.Sun, RunePillar.Rune.Moon, RunePillar.Rune.Star };

        /// <summary>Raised when the challenge is finished in the loaded scene (the mirror room opens).</summary>
        public static event System.Action Completed;

        [SerializeField] private List<RunePillar> pillars = new List<RunePillar>();
        [Tooltip("Rewards locked until the challenge is done (the elixir pedestal and the ring).")]
        [SerializeField] private List<Interactable> sealedRewards = new List<Interactable>();
        [Tooltip("Shimmering seal over the pedestal, removed when the challenge is done.")]
        [SerializeField] private GameObject sealVisual;
        [Tooltip("The mimic's encounter room (an optional fight, only remembered).")]
        [SerializeField] private DungeonRoomController guardianRoom;

        public List<RunePillar> Pillars => pillars;
        public List<Interactable> SealedRewards => sealedRewards;

        public GameObject SealVisual
        {
            get => sealVisual;
            set => sealVisual = value;
        }

        public DungeonRoomController GuardianRoom
        {
            get => guardianRoom;
            set => guardianRoom = value;
        }

        /// <summary>Whether the rune pillars are solved.</summary>
        public bool RunesSolved => StoryFlags.Has(RunesSolvedFlag);

        /// <summary>Whether the mimic (the optional treasure guardian) has been beaten.</summary>
        public bool GuardianDefeated => StoryFlags.Has(GuardianDefeatedFlag);

        /// <summary>Whether the whole challenge is done and the rewards unsealed.</summary>
        public bool IsComplete => StoryFlags.Has(CompletedFlag);

        private void OnEnable()
        {
            DungeonRoomController.OnRoomCleared += HandleRoomCleared;
        }

        private void OnDisable()
        {
            DungeonRoomController.OnRoomCleared -= HandleRoomCleared;
        }

        private void Start()
        {
            for (int i = 0; i < pillars.Count; i++)
            {
                if (pillars[i] != null) pillars[i].Challenge = this;
            }
            if (RunesSolved)
            {
                for (int i = 0; i < pillars.Count && i < Solution.Length; i++)
                {
                    if (pillars[i] != null) pillars[i].SetRune(Solution[i]);
                }
            }
            ApplySeal();

            // Saves from before the mimic was made optional: the runes alone now finish the challenge
            if (RunesSolved && !IsComplete) TryComplete();
        }

        /// <summary>Called by a pillar after it turns.</summary>
        public void OnPillarTurned()
        {
            if (RunesSolved || pillars.Count < Solution.Length) return;
            for (int i = 0; i < Solution.Length; i++)
            {
                if (pillars[i] == null || pillars[i].Current != Solution[i]) return;
            }

            StoryFlags.Set(RunesSolvedFlag);
            for (int i = 0; i < pillars.Count; i++)
            {
                if (pillars[i] != null) pillars[i].Lock();
            }
            TryComplete();
        }

        private void HandleRoomCleared(DungeonRoomController room)
        {
            if (room == null || room != guardianRoom) return;
            StoryFlags.Set(GuardianDefeatedFlag);
        }

        /// <summary>Unseals the rewards and grants the level once the runes are solved.</summary>
        public void TryComplete()
        {
            if (IsComplete || !RunesSolved) return;

            StoryFlags.Set(CompletedFlag);
            ApplySeal();
            Notify("The runes blaze in order: SUN, MOON, STAR. The seal breaks: the Giant's Elixir and the Queen's ring are yours to take.");
            PlayerProgressionManager.Instance?.GrantBonusLevel("Treasure Tower challenge completed");
            Completed?.Invoke();
        }

        private void ApplySeal()
        {
            bool open = IsComplete;
            for (int i = 0; i < sealedRewards.Count; i++)
            {
                Interactable reward = sealedRewards[i];
                if (reward == null) continue;
                reward.IsInteractable = open;
            }
            if (sealVisual != null) sealVisual.SetActive(!open);
        }

        private static void Notify(string message)
        {
            Debug.Log("[TowerChallenge] " + message);
            UI.LockpickMinigameUI.ShowToast(message);
        }
    }
}
