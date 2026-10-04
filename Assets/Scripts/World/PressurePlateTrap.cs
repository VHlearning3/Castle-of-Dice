using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// A pressure plate trap in the Treasure Tower (critical review C5). Stepping on it while exploring calls
    /// for a DEX save (DC 12, can be rerolled with a scroll); a failure means darts for 1d6 damage. Each
    /// plate springs once per visit, and the plate sinks so the player can see it was used.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class PressurePlateTrap : MonoBehaviour
    {
        public const int SaveDC = 12;
        public const int DamageDieSides = 6;

        [SerializeField] private Transform plateVisual;

        private bool sprung;

        /// <summary>Whether the plate already went off this visit.</summary>
        public bool IsSprung => sprung;

        public Transform PlateVisual
        {
            get => plateVisual;
            set => plateVisual = value;
        }

        private void Reset()
        {
            BoxCollider col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (sprung) return;
            PlayerUnit hero = other.GetComponent<PlayerUnit>() ?? other.GetComponentInParent<PlayerUnit>();
            if (hero == null) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentMode == GamePlayMode.Combat) return;
            Spring(hero);
        }

        /// <summary>Springs the trap on <paramref name="hero"/>.</summary>
        public void Spring(PlayerUnit hero)
        {
            if (sprung || hero == null || !hero.IsAlive) return;
            sprung = true;
            if (plateVisual != null) plateVisual.localPosition += Vector3.down * 0.05f;

            UI.LockpickMinigameUI.ShowToast("Click! A pressure plate. Dodge the darts (DEX DC 12)!");
            int dex = HeroAttributes.GetModifier(hero, HeroAttribute.Dexterity);
            RerollableRoll.Roll(dex, SaveDC, AdvantageType.None, save =>
            {
                if (save.isSuccess)
                {
                    UI.LockpickMinigameUI.ShowToast("You twist aside as the darts hiss past.");
                    return;
                }
                int damage = DiceSystem.RollDice(DamageDieSides);
                UI.LockpickMinigameUI.ShowToast($"The darts hit you for {damage}!");
                hero.TakeDamage(damage);
            }, "Dart Trap / Dexterity");
        }
    }
}
