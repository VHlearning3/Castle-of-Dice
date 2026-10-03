using UnityEngine;

namespace CastleOfTheD20.Bosses
{
    /// <summary>
    /// Swaps the Petrification King's first-stage golem model for the second-stage one when Stone Form
    /// (phase 2) starts, and hands the new model's Animator to the boss.
    /// </summary>
    [RequireComponent(typeof(GargoyleKingBoss))]
    public class GargoyleKingStageModels : MonoBehaviour
    {
        [SerializeField] private GameObject stage1Model;
        [SerializeField] private GameObject stage2Model;

        private GargoyleKingBoss boss;

        private void Awake()
        {
            boss = GetComponent<GargoyleKingBoss>();
            ShowStage(boss != null && boss.IsStoneFormActive ? 2 : 1);
        }

        private void OnEnable()
        {
            GargoyleKingBoss.OnStoneFormActivated += HandleStoneForm;
            GargoyleKingBoss.OnStoneFormReset += HandleStoneFormReset;
        }

        private void OnDisable()
        {
            GargoyleKingBoss.OnStoneFormActivated -= HandleStoneForm;
            GargoyleKingBoss.OnStoneFormReset -= HandleStoneFormReset;
        }

        private void HandleStoneFormReset(GargoyleKingBoss source)
        {
            if (source == boss) ShowStage(1);
        }

        private void HandleStoneForm(GargoyleKingBoss source)
        {
            if (source == boss) ShowStage(2);
        }

        private void ShowStage(int stage)
        {
            GameObject shown = stage == 2 && stage2Model != null ? stage2Model : stage1Model;
            if (shown == null) return;

            if (stage1Model != null) stage1Model.SetActive(stage1Model == shown);
            if (stage2Model != null) stage2Model.SetActive(stage2Model == shown);

            if (boss != null)
            {
                Animator animator = shown.GetComponentInChildren<Animator>(true);
                if (animator != null) boss.UnitAnimator = animator;
            }
        }
    }
}
