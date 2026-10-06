using UnityEngine;
using UnityEngine.EventSystems;

namespace CastleOfTheD20.UI
{
    /// <summary>Hovering a button on the combat action row (Dash, Dodge, ...) explains it on the hint line.</summary>
    public class RowActionHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>The combat UI that owns the hint line.</summary>
        public CombatUIController Owner { get; set; }

        /// <summary>Which row action this button is.</summary>
        public CombatUIController.RowAction Action { get; set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Owner != null) Owner.ShowRowHint(Action);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Owner != null) Owner.ShowRowHint(null);
        }

        private void OnDisable()
        {
            if (Owner != null) Owner.ShowRowHint(null);
        }
    }
}
