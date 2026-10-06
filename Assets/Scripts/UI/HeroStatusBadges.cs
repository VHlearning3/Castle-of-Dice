using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.World;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Small pills under the hero card (Vili, 2026-10-06): INSPIRED while the hero holds Heroic Inspiration,
    /// SNEAKING (V) while sneaking, UNSEEN while the Shadow Cloak is worn, and a [C] reminder for the character
    /// sheet. Only booleans are compared each frame; a pill is shown or hidden when its state changes.
    /// </summary>
    public class HeroStatusBadges : MonoBehaviour
    {
        public const string RootName = "Hero_Status_Badges";

        private static readonly Color InspiredColor = new Color(0.86f, 0.66f, 0.18f, 0.95f);
        private static readonly Color SneakColor = new Color(0.35f, 0.28f, 0.55f, 0.95f);
        private static readonly Color UnseenColor = new Color(0.16f, 0.14f, 0.24f, 0.95f);
        private static readonly Color SheetColor = new Color(0.20f, 0.17f, 0.12f, 0.9f);

        private GameObject inspiredPill;
        private GameObject sneakPill;
        private GameObject unseenPill;

        private bool shownInspired;
        private bool shownSneak;
        private bool shownUnseen;

        /// <summary>Builds the badges under <paramref name="hud"/> (the PlayerHUD strip) once.</summary>
        public static HeroStatusBadges Ensure(Transform hud, Sprite pillSprite)
        {
            if (hud == null) return null;
            Transform existing = hud.Find(RootName);
            if (existing != null)
            {
                HeroStatusBadges found = existing.GetComponent<HeroStatusBadges>();
                if (found != null) return found;
            }

            GameObject root = existing != null ? existing.gameObject : new GameObject(RootName, typeof(RectTransform));
            root.transform.SetParent(hud, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -252f); // under the 226 px hero card at y -18
            rect.sizeDelta = new Vector2(460f, 28f);

            HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>() ?? root.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            HeroStatusBadges badges = root.GetComponent<HeroStatusBadges>() ?? root.AddComponent<HeroStatusBadges>();
            badges.Build(pillSprite);
            return badges;
        }

        private void Build(Sprite pillSprite)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }

            Pill("Badge_Sheet", "[C] Character", SheetColor, pillSprite, 118f).SetActive(true);
            inspiredPill = Pill("Badge_Inspired", "INSPIRED", InspiredColor, pillSprite, 100f);
            sneakPill = Pill("Badge_Sneaking", "SNEAKING (V)", SneakColor, pillSprite, 126f);
            unseenPill = Pill("Badge_Unseen", "UNSEEN (cloak)", UnseenColor, pillSprite, 130f);
            shownInspired = shownSneak = shownUnseen = false;
        }

        private GameObject Pill(string objectName, string label, Color color, Sprite sprite, float width)
        {
            GameObject pill = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pill.transform.SetParent(transform, false);
            ((RectTransform)pill.transform).sizeDelta = new Vector2(width, 26f);
            Image image = pill.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(pill.transform, false);
            RectTransform textRect = (RectTransform)textObj.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TMP_Text text = textObj.GetComponent<TMP_Text>();
            text.text = label;
            text.fontSize = 13f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = UITheme.CreamText;
            text.raycastTarget = false;

            pill.SetActive(false);
            return pill;
        }

        private void Update()
        {
            bool inspired = HeroicInspiration.Has;
            if (inspired != shownInspired && inspiredPill != null)
            {
                shownInspired = inspired;
                inspiredPill.SetActive(inspired);
            }

            bool sneaking = PlayerExplorationMovement.IsSneaking;
            if (sneaking != shownSneak && sneakPill != null)
            {
                shownSneak = sneaking;
                sneakPill.SetActive(sneaking);
            }

            bool unseen = Economy.MagicItems.IsUnseen;
            if (unseen != shownUnseen && unseenPill != null)
            {
                shownUnseen = unseen;
                unseenPill.SetActive(unseen);
            }
        }
    }
}
