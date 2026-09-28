#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Tests
{
    [TestFixture]
    public class UIThemeTests
    {
        private static readonly string[] CanonicalSpritePaths = new string[]
        {
            "Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png",
            "Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png",
            "Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png",
            "Assets/UI/Sprites/UI_Fantasy_Pill_Badge.png",
            "Assets/UI/Sprites/UI_Fantasy_Portrait_Placeholder.png",
            "Assets/UI/Sprites/UI_Fantasy_Button_Normal.png",
            "Assets/UI/Sprites/UI_Fantasy_Button_Hover.png",
            "Assets/UI/Sprites/UI_Fantasy_Button_Pressed.png",
            "Assets/UI/Sprites/UI_Fantasy_Bar_Track.png",
            "Assets/UI/Sprites/UI_Fantasy_Bar_Fill_Ruby.png",
            "Assets/UI/Sprites/UI_Fantasy_Crest_Plate.png",
            "Assets/UI/Sprites/UI_Fantasy_Crest_Warrior.png",
            "Assets/UI/Sprites/UI_Fantasy_Crest_Mage.png",
            "Assets/UI/Sprites/UI_Fantasy_Crest_Rogue.png",
            "Assets/UI/Sprites/UI_Icon_Sword.png",
            "Assets/UI/Sprites/UI_Icon_Shield.png",
            "Assets/UI/Sprites/UI_Icon_Hourglass.png",
            "Assets/UI/Sprites/UI_Icon_ScrapOre.png",
            "Assets/ICONSART/CoinIcon.png",
            "Assets/ICONSART/HealthPotionIcon.png"
        };

        [Test]
        public void UITheme_Active_LoadsFromResources()
        {
            UITheme theme = UITheme.Active;
            Assert.IsNotNull(theme, "UITheme.Active must load successfully from Resources/UITheme.asset");
        }

        [Test]
        public void UITheme_All20Sprites_AreAssignedInAsset()
        {
            UITheme theme = UITheme.Active;
            Assert.IsNotNull(theme, "UITheme.Active is null.");

            Assert.IsNotNull(theme.panelDark, "theme.panelDark is null.");
            Assert.IsNotNull(theme.slotFrame, "theme.slotFrame is null.");
            Assert.IsNotNull(theme.dividerGold, "theme.dividerGold is null.");
            Assert.IsNotNull(theme.pillBadge, "theme.pillBadge is null.");
            Assert.IsNotNull(theme.portraitPlaceholder, "theme.portraitPlaceholder is null.");

            Assert.IsNotNull(theme.buttonNormal, "theme.buttonNormal is null.");
            Assert.IsNotNull(theme.buttonHover, "theme.buttonHover is null.");
            Assert.IsNotNull(theme.buttonPressed, "theme.buttonPressed is null.");

            Assert.IsNotNull(theme.barTrack, "theme.barTrack is null.");
            Assert.IsNotNull(theme.barFillRuby, "theme.barFillRuby is null.");
            Assert.IsNotNull(theme.crestPlate, "theme.crestPlate is null.");
            Assert.IsNotNull(theme.crestWarrior, "theme.crestWarrior is null.");
            Assert.IsNotNull(theme.crestMage, "theme.crestMage is null.");
            Assert.IsNotNull(theme.crestRogue, "theme.crestRogue is null.");

            Assert.IsNotNull(theme.iconSword, "theme.iconSword is null.");
            Assert.IsNotNull(theme.iconShield, "theme.iconShield is null.");
            Assert.IsNotNull(theme.iconHourglass, "theme.iconHourglass is null.");
            Assert.IsNotNull(theme.iconScrapOre, "theme.iconScrapOre is null.");
            Assert.IsNotNull(theme.iconCoin, "theme.iconCoin is null.");
            Assert.IsNotNull(theme.iconHealthPotion, "theme.iconHealthPotion is null.");
        }

        [Test]
        public void UITheme_GetSprite_ResolvesAll20CanonicalPaths()
        {
            foreach (string path in CanonicalSpritePaths)
            {
                Sprite sprite = UITheme.GetSprite(path);
                Assert.IsNotNull(sprite, $"UITheme.GetSprite('{path}') returned null.");
            }
        }

        [Test]
        public void UITheme_Palette_ColorsAreValid()
        {
            Assert.Greater(UITheme.GoldAccent.a, 0f);
            Assert.Greater(UITheme.ParchmentText.a, 0f);
            Assert.Greater(UITheme.CoinGold.a, 0f);
            Assert.Greater(UITheme.CreamText.a, 0f);
            Assert.Greater(UITheme.SoftText.a, 0f);
            Assert.Greater(UITheme.PanelSlate.a, 0f);
            Assert.Greater(UITheme.PanelAbyss.a, 0f);
            Assert.Greater(UITheme.ActionGreen.a, 0f);
        }

        [Test]
        public void UITheme_Controllers_CanLoadThemeSpritesWithoutException()
        {
            GameObject go = new GameObject("Test_QuestHUD", typeof(QuestHUDUIController));
            try
            {
                QuestHUDUIController questHUD = go.GetComponent<QuestHUDUIController>();
                Assert.DoesNotThrow(() => questHUD.LoadThemeSpritesIfMissing());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
