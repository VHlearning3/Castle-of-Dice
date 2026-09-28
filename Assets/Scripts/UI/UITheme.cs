using UnityEngine;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Shared fantasy UI theme: every themed sprite and the recurring palette colours in one asset.
    /// Loaded from Resources/UITheme so procedurally styled panels get the same sprites in WebGL builds
    /// as in the Editor (UnityEditor.AssetDatabase is not available in player builds).
    /// Generate or refresh the asset with CastleOfDice/Generate UI Theme Asset.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "CastleOfDice/UI/UI Theme", order = 30)]
    public class UITheme : ScriptableObject
    {
        public const string ResourcePath = "UITheme";

        #region Sprites

        [Header("Panels & Frames")]
        public Sprite panelDark;
        public Sprite slotFrame;
        public Sprite dividerGold;
        public Sprite pillBadge;
        public Sprite portraitPlaceholder;

        [Header("Buttons")]
        public Sprite buttonNormal;
        public Sprite buttonHover;
        public Sprite buttonPressed;

        [Header("Bars & Crests")]
        public Sprite barTrack;
        public Sprite barFillRuby;
        public Sprite crestPlate;
        public Sprite crestWarrior;
        public Sprite crestMage;
        public Sprite crestRogue;

        [Header("Icons")]
        public Sprite iconSword;
        public Sprite iconShield;
        public Sprite iconHourglass;
        public Sprite iconScrapOre;
        public Sprite iconCoin;
        public Sprite iconHealthPotion;

        #endregion

        #region Palette

        /// <summary>Warm gold used for titles and highlighted labels.</summary>
        public static readonly Color GoldAccent = new Color(0.965f, 0.835f, 0.47f, 1f);

        /// <summary>Parchment body text.</summary>
        public static readonly Color ParchmentText = new Color(0.93f, 0.90f, 0.85f, 1f);

        /// <summary>Bright coin gold (level-up banners, gold counters).</summary>
        public static readonly Color CoinGold = new Color(1f, 0.84f, 0f, 1f);

        /// <summary>Cream highlight text.</summary>
        public static readonly Color CreamText = new Color(1.0f, 0.96f, 0.85f, 1f);

        /// <summary>Cool light-grey secondary text.</summary>
        public static readonly Color SoftText = new Color(0.85f, 0.88f, 0.92f, 1f);

        /// <summary>Dark slate panel background.</summary>
        public static readonly Color PanelSlate = new Color(0.12f, 0.14f, 0.18f, 0.95f);

        /// <summary>Near-black modal backdrop.</summary>
        public static readonly Color PanelAbyss = new Color(0.04f, 0.05f, 0.08f, 0.98f);

        /// <summary>Green for confirm / new-game actions.</summary>
        public static readonly Color ActionGreen = new Color(0.2f, 0.55f, 0.3f);

        #endregion

        #region Runtime Access

        private static UITheme s_active;

        /// <summary>The theme asset in Resources (null only if it has not been generated yet).</summary>
        public static UITheme Active
        {
            get
            {
                if (s_active == null)
                {
                    s_active = Resources.Load<UITheme>(ResourcePath);
                }
                return s_active;
            }
        }

        /// <summary>
        /// Resolves a themed sprite by its project asset path. Works in player builds via the theme asset;
        /// in the Editor it falls back to loading the file directly if the theme lacks the sprite.
        /// </summary>
        public static Sprite GetSprite(string assetPath)
        {
            Sprite sprite = null;
            UITheme theme = Active;
            if (theme != null)
            {
                sprite = theme.FindByPath(assetPath);
            }

#if UNITY_EDITOR
            if (sprite == null)
            {
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }
#endif
            if (sprite == null)
            {
                Debug.LogWarning($"[UITheme] Sprite '{assetPath}' is not in the UI theme. Run CastleOfDice/Generate UI Theme Asset.");
            }
            return sprite;
        }

        private Sprite FindByPath(string assetPath)
        {
            switch (assetPath)
            {
                case "Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png": return panelDark;
                case "Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png": return slotFrame;
                case "Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png": return dividerGold;
                case "Assets/UI/Sprites/UI_Fantasy_Pill_Badge.png": return pillBadge;
                case "Assets/UI/Sprites/UI_Fantasy_Portrait_Placeholder.png": return portraitPlaceholder;
                case "Assets/UI/Sprites/UI_Fantasy_Button_Normal.png": return buttonNormal;
                case "Assets/UI/Sprites/UI_Fantasy_Button_Hover.png": return buttonHover;
                case "Assets/UI/Sprites/UI_Fantasy_Button_Pressed.png": return buttonPressed;
                case "Assets/UI/Sprites/UI_Fantasy_Bar_Track.png": return barTrack;
                case "Assets/UI/Sprites/UI_Fantasy_Bar_Fill_Ruby.png": return barFillRuby;
                case "Assets/UI/Sprites/UI_Fantasy_Crest_Plate.png": return crestPlate;
                case "Assets/UI/Sprites/UI_Fantasy_Crest_Warrior.png": return crestWarrior;
                case "Assets/UI/Sprites/UI_Fantasy_Crest_Mage.png": return crestMage;
                case "Assets/UI/Sprites/UI_Fantasy_Crest_Rogue.png": return crestRogue;
                case "Assets/UI/Sprites/UI_Icon_Sword.png": return iconSword;
                case "Assets/UI/Sprites/UI_Icon_Shield.png": return iconShield;
                case "Assets/UI/Sprites/UI_Icon_Hourglass.png": return iconHourglass;
                case "Assets/UI/Sprites/UI_Icon_ScrapOre.png": return iconScrapOre;
                case "Assets/ICONSART/CoinIcon.png": return iconCoin;
                case "Assets/ICONSART/HealthPotionIcon.png": return iconHealthPotion;
                default: return null;
            }
        }

        #endregion
    }
}
