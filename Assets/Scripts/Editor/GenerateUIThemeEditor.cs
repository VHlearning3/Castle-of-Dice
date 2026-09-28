using System.IO;
using UnityEngine;
using UnityEditor;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Editor utility to generate or refresh the singleton Resources/UITheme.asset
    /// with serialized references to all 20 procedural UI fantasy sprites and icons.
    /// This ensures WebGL builds have zero AssetDatabase dependencies at runtime.
    /// </summary>
    public static class GenerateUIThemeEditor
    {
        private const string ResourcesDir = "Assets/Resources";
        private const string ThemeAssetPath = "Assets/Resources/UITheme.asset";

        [MenuItem("CastleOfDice/Generate UI Theme Asset", false, 15)]
        public static void GenerateUITheme()
        {
            // 1. Ensure Resources directory exists
            if (!Directory.Exists(ResourcesDir))
            {
                Directory.CreateDirectory(ResourcesDir);
                AssetDatabase.Refresh();
            }

            // 2. Ensure fantasy UI sprites exist; if missing, generate them first
            if (!File.Exists("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png"))
            {
                GenerateFantasyUISpritesEditor.GenerateAllSprites();
            }

            // 3. Load or create UITheme ScriptableObject asset
            UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemeAssetPath);
            bool isNew = false;
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme>();
                AssetDatabase.CreateAsset(theme, ThemeAssetPath);
                isNew = true;
            }

            // 4. Assign all 20 sprites
            theme.panelDark = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            theme.slotFrame = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            theme.dividerGold = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            theme.pillBadge = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Pill_Badge.png");
            theme.portraitPlaceholder = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Portrait_Placeholder.png");

            theme.buttonNormal = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Button_Normal.png");
            theme.buttonHover = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Button_Hover.png");
            theme.buttonPressed = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Button_Pressed.png");

            theme.barTrack = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Bar_Track.png");
            theme.barFillRuby = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Bar_Fill_Ruby.png");
            theme.crestPlate = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Crest_Plate.png");
            theme.crestWarrior = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Crest_Warrior.png");
            theme.crestMage = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Crest_Mage.png");
            theme.crestRogue = LoadSprite("Assets/UI/Sprites/UI_Fantasy_Crest_Rogue.png");

            theme.iconSword = LoadSprite("Assets/UI/Sprites/UI_Icon_Sword.png");
            theme.iconShield = LoadSprite("Assets/UI/Sprites/UI_Icon_Shield.png");
            theme.iconHourglass = LoadSprite("Assets/UI/Sprites/UI_Icon_Hourglass.png");
            theme.iconScrapOre = LoadSprite("Assets/UI/Sprites/UI_Icon_ScrapOre.png");
            theme.iconCoin = LoadSprite("Assets/ICONSART/CoinIcon.png");
            theme.iconHealthPotion = LoadSprite("Assets/ICONSART/HealthPotionIcon.png");

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GenerateUIThemeEditor] Successfully {(isNew ? "created" : "updated")} UITheme asset at {ThemeAssetPath} with 20 sprites.");
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[GenerateUIThemeEditor] Failed to load sprite at '{path}'. Verify file exists and is imported as Sprite (2D and UI).");
            }
            return sprite;
        }
    }
}
