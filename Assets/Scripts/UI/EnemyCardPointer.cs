using UnityEngine;
using UnityEngine.EventSystems;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Sits on each enemy stat card: hovering the card lights up that enemy's model (and the card),
    /// clicking it keeps the highlight until something else is clicked.
    /// </summary>
    public class EnemyCardPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        /// <summary>The HUD that owns the card.</summary>
        public CombatStatsHUD Owner;

        /// <summary>Which card this is (0 = leftmost).</summary>
        public int Index;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Owner != null) EnemyFocus.CardHovered = Owner.GetCardEnemy(Index);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Owner != null && EnemyFocus.CardHovered == Owner.GetCardEnemy(Index))
            {
                EnemyFocus.CardHovered = null;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Owner != null) EnemyFocus.Selected = Owner.GetCardEnemy(Index);
        }

        private void OnDisable()
        {
            // A card that hides (its enemy died) must not leave a hover behind
            if (Owner != null && EnemyFocus.CardHovered != null && EnemyFocus.CardHovered == Owner.GetCardEnemy(Index))
            {
                EnemyFocus.CardHovered = null;
            }
        }
    }
}
