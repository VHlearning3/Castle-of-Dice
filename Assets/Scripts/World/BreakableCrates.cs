using System.Collections;
using UnityEngine;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The knight's own quest (Vili, 2026-10-06): a wall of boarded-up crates closes off a corner of the
    /// Castle Hall. Only the warrior can smash them; behind them waits the King's old armoury chest. The
    /// broken state is a story flag, so the crates stay broken on every later visit.
    /// </summary>
    public class BreakableCrates : Interactable
    {
        /// <summary>Story flag set once the crates are smashed.</summary>
        public const string BrokenFlag = "KnightCratesBroken";

        /// <summary>Shown to the mage and the rogue.</summary>
        public const string TooSturdyMessage = "Nailed shut with oak a hand thick.\nOnly a knight's strength could break these.";

        [Tooltip("The crate models (each flies apart when smashed).")]
        [SerializeField] private Transform[] crates = new Transform[0];

        [Tooltip("Solid colliders that keep everyone out of the corner until the crates are gone.")]
        [SerializeField] private Collider[] blockers = new Collider[0];

        private bool broken;

        public Transform[] Crates
        {
            get => crates;
            set => crates = value ?? new Transform[0];
        }

        public Collider[] Blockers
        {
            get => blockers;
            set => blockers = value ?? new Collider[0];
        }

        /// <summary>Whether the crates have been smashed.</summary>
        public bool IsBroken => broken;

        protected override void Awake()
        {
            base.Awake();
            interactionRadius = Mathf.Max(interactionRadius, 3f);
            if (StoryFlags.Has(BrokenFlag)) SetBroken(true);
            else promptMessage = "Smash the boarded-up crates";
        }

        public override bool CanInteract(PlayerUnit player)
        {
            return !broken && base.CanInteract(player);
        }

        public override void Interact(PlayerUnit player)
        {
            if (broken) return;
            if (!IsKnight(player))
            {
                LockpickMinigameUI.ShowToast(TooSturdyMessage);
                SFXManager.Instance?.PlaySFX(SFXClipType.CriticalFailure, transform.position);
                return;
            }

            StoryFlags.Set(BrokenFlag);
            SFXManager.Instance?.PlaySFX(SFXClipType.SwordHit, transform.position);
            if (Application.isPlaying) AbilityVfx.PlayCameraShake(0.35f, 0.18f);
            LockpickMinigameUI.ShowToast("The boards splinter apart!");
            if (Application.isPlaying) StartCoroutine(Shatter());
            else SetBroken(true);
        }

        /// <summary>Only the warrior can break the crates.</summary>
        public static bool IsKnight(PlayerUnit player)
        {
            return player != null && player.CharacterClass != null && player.CharacterClass.ClassType == CharacterClassType.Warrior;
        }

        private IEnumerator Shatter()
        {
            broken = true;
            isInteractable = false;
            SetBlockers(false);

            Vector3 centre = transform.position;
            Vector3[] start = new Vector3[crates.Length];
            Vector3[] dir = new Vector3[crates.Length];
            for (int i = 0; i < crates.Length; i++)
            {
                if (crates[i] == null) continue;
                start[i] = crates[i].position;
                Vector3 away = crates[i].position - centre;
                away.y = 0f;
                dir[i] = (away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere) * Random.Range(1.5f, 3f);
            }

            const float duration = 0.6f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                for (int i = 0; i < crates.Length; i++)
                {
                    if (crates[i] == null) continue;
                    crates[i].position = start[i] + dir[i] * k + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 1.2f);
                    crates[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, k * k);
                    crates[i].Rotate(new Vector3(260f, 140f, 90f) * Time.deltaTime, Space.Self);
                }
                yield return null;
            }
            SetBroken(true);
        }

        private void SetBroken(bool value)
        {
            broken = value;
            isInteractable = !value;
            SetBlockers(!value);
            for (int i = 0; i < crates.Length; i++)
            {
                if (crates[i] != null) crates[i].gameObject.SetActive(!value);
            }
            promptMessage = value ? string.Empty : "Smash the boarded-up crates";
        }

        private void SetBlockers(bool on)
        {
            for (int i = 0; i < blockers.Length; i++)
            {
                if (blockers[i] != null) blockers[i].enabled = on;
            }
        }
    }
}
