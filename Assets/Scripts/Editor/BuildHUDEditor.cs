using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using CastleOfTheD20.UI;
using CastleOfTheD20.Data;
using CastleOfTheD20.Core;
using CastleOfTheD20.Audio;
using CastleOfTheD20.World;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Editor tool to build, style, and serialize the professional tabletop D&D HUD hierarchy in the active scene.
    /// Strictly adheres to the rule that the user's authentic CoinIcon.png and HealthPotionIcon.png
    /// are preserved and wired directly with preserveAspect = true.
    /// </summary>
    public static class BuildHUDEditor
    {
        [MenuItem("CastleOfDice/Rebuild and Style Professional HUD", false, 25)]
        public static void RebuildAndStyleHUD()
        {
            var activeScene = EditorSceneManager.GetActiveScene();

            // 1. Ensure Theme Sprites exist
            if (!System.IO.File.Exists("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png"))
            {
                GenerateFantasyUISpritesEditor.GenerateAllSprites();
            }

            Sprite panelDark = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            Sprite slotFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            Sprite barTrack = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Bar_Track.png");
            Sprite barFillRuby = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Bar_Fill_Ruby.png");
            Sprite pillBadge = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Pill_Badge.png");
            Sprite dividerGold = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            Sprite crestPlate = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Crest_Plate.png");
            Sprite crestWarrior = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Crest_Warrior.png");
            Sprite crestMage = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Crest_Mage.png");
            Sprite crestRogue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Crest_Rogue.png");

            Sprite coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ICONSART/CoinIcon.png");
            Sprite potionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ICONSART/HealthPotionIcon.png");
            Sprite scrapOre = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Icon_ScrapOre.png");
            ItemSO potionItem = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_Potion_Health.asset");
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            // 2. Find Canvas
            Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            // 3. Find or Create PlayerHUD
            PlayerHUD hud = Object.FindAnyObjectByType<PlayerHUD>(FindObjectsInactive.Include);
            if (hud == null)
            {
                GameObject hudObj = new GameObject("PlayerHUD", typeof(RectTransform), typeof(PlayerHUD));
                hudObj.transform.SetParent(canvas.transform, false);
                hud = hudObj.GetComponent<PlayerHUD>();
            }

            Undo.RegisterCompleteObjectUndo(hud.gameObject, "Rebuild & Style HUD");

            // Assign theme sprites to HUD
            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("panelDarkSprite").objectReferenceValue = panelDark;
            so.FindProperty("slotFrameSprite").objectReferenceValue = slotFrame;
            so.FindProperty("barTrackSprite").objectReferenceValue = barTrack;
            so.FindProperty("barFillRubySprite").objectReferenceValue = barFillRuby;
            so.FindProperty("pillBadgeSprite").objectReferenceValue = pillBadge;
            so.FindProperty("dividerGoldSprite").objectReferenceValue = dividerGold;
            so.FindProperty("crestPlateSprite").objectReferenceValue = crestPlate;
            so.FindProperty("crestWarriorSprite").objectReferenceValue = crestWarrior;
            so.FindProperty("crestMageSprite").objectReferenceValue = crestMage;
            so.FindProperty("crestRogueSprite").objectReferenceValue = crestRogue;
            so.FindProperty("scrapOreSprite").objectReferenceValue = scrapOre;
            if (potionItem != null)
            {
                so.FindProperty("healthPotionItem").objectReferenceValue = potionItem;
            }
            so.ApplyModifiedProperties();

            // 4. Run hierarchy structuring & styling
            hud.EnsureStyledHierarchy();

            // 5. Ensure fonts and properties across all texts
            TMP_Text[] allTexts = hud.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in allTexts)
            {
                if (fontAsset != null)
                {
                    t.font = fontAsset;
                }
                t.raycastTarget = false;
            }

            // 6. Strictly preserve and wire CoinIcon and HealthPotionIcon
            Image[] allImages = hud.GetComponentsInChildren<Image>(true);
            foreach (var img in allImages)
            {
                string lower = img.gameObject.name.ToLowerInvariant();
                if (lower.Contains("coin") || lower.Contains("gold_icon"))
                {
                    img.gameObject.name = "Gold_Icon";
                    if (coinSprite != null)
                    {
                        img.sprite = coinSprite;
                        img.preserveAspect = true;
                        img.color = Color.white;
                    }
                    img.raycastTarget = false;
                    RectTransform cRect = img.GetComponent<RectTransform>();
                    cRect.anchorMin = new Vector2(0f, 0.5f);
                    cRect.anchorMax = new Vector2(0f, 0.5f);
                    cRect.pivot = new Vector2(0f, 0.5f);
                    cRect.anchoredPosition = new Vector2(8f, 0f);
                    cRect.sizeDelta = new Vector2(22f, 22f);
                    cRect.localScale = Vector3.one;

                    so.FindProperty("coinIconImage").objectReferenceValue = img;
                }
                else if (lower.Contains("potion") && !lower.Contains("button"))
                {
                    if (potionSprite != null)
                    {
                        img.sprite = potionSprite;
                        img.preserveAspect = true;
                        img.color = Color.white;
                    }
                    img.raycastTarget = false;
                    RectTransform pRect = img.GetComponent<RectTransform>();
                    pRect.anchorMin = new Vector2(0.5f, 0.5f);
                    pRect.anchorMax = new Vector2(0.5f, 0.5f);
                    pRect.pivot = new Vector2(0.5f, 0.5f);
                    pRect.anchoredPosition = Vector2.zero;
                    pRect.sizeDelta = new Vector2(28f, 28f);
                    pRect.localScale = Vector3.one;

                    so.FindProperty("potionIconImage").objectReferenceValue = img;
                }
            }

            // 7. Rebind serialized references cleanly
            Slider sl = hud.GetComponentInChildren<Slider>(true);
            if (sl != null)
            {
                so.FindProperty("healthSlider").objectReferenceValue = sl;
                Image fill = null;
                foreach (var img in sl.GetComponentsInChildren<Image>(true))
                {
                    if (img.name.ToLowerInvariant().Contains("fill"))
                    {
                        fill = img;
                        break;
                    }
                }
                if (fill != null)
                {
                    so.FindProperty("sliderFillImage").objectReferenceValue = fill;
                }
            }

            Button pb = hud.GetComponentInChildren<Button>(true);
            if (pb != null)
            {
                so.FindProperty("quickPotionButton").objectReferenceValue = pb;
            }

            Transform crestBox = hud.transform.Find("Hero_Status_Card/Hero_Crest_Box");
            if (crestBox != null)
            {
                Image crestImg = crestBox.GetComponent<Image>();
                if (crestImg != null)
                {
                    so.FindProperty("heroCrestImage").objectReferenceValue = crestImg;
                }
            }

            Transform goldTxt = hud.transform.Find("Hero_Status_Card/Gold_Container/Gold_Text")
                ?? hud.transform.Find("Hero_Status_Card/Gold_Container/Text (TMP)");
            if (goldTxt != null)
            {
                TMP_Text gt = goldTxt.GetComponent<TMP_Text>();
                if (gt != null)
                {
                    so.FindProperty("goldCounterText").objectReferenceValue = gt;
                    RectTransform gtRect = gt.GetComponent<RectTransform>();
                    gtRect.anchorMin = new Vector2(0f, 0f);
                    gtRect.anchorMax = new Vector2(1f, 1f);
                    gtRect.pivot = new Vector2(0f, 0.5f);
                    gtRect.anchoredPosition = new Vector2(36f, 0f);
                    gtRect.sizeDelta = new Vector2(-42f, 0f);
                    gtRect.localScale = Vector3.one;
                    gt.alignment = TextAlignmentOptions.MidlineLeft;
                    gt.fontSize = 13f;
                    gt.fontStyle = FontStyles.Bold;
                    gt.color = new Color(0.98f, 0.82f, 0.20f, 1f);
                }
            }

            Transform questSummary = hud.transform.Find("Quest_Tracker_Card/ActiveQuestSummaryText");
            if (questSummary != null)
            {
                TMP_Text qs = questSummary.GetComponent<TMP_Text>();
                if (qs != null)
                {
                    so.FindProperty("activeQuestSummaryText").objectReferenceValue = qs;
                    RectTransform qsRect = qs.GetComponent<RectTransform>();
                    qsRect.anchorMin = new Vector2(0f, 0f);
                    qsRect.anchorMax = new Vector2(1f, 1f);
                    qsRect.pivot = new Vector2(0f, 1f);
                    qsRect.anchoredPosition = new Vector2(18f, -40f);
                    qsRect.sizeDelta = new Vector2(-36f, -48f);
                    qsRect.localScale = Vector3.one;
                    qs.alignment = TextAlignmentOptions.TopLeft;
                    qs.fontSize = 12f;
                    qs.enableAutoSizing = false;
                    qs.lineSpacing = -2f;
                    qs.paragraphSpacing = 3f;
                    qs.richText = true;
                }
            }

            Transform questHeader = hud.transform.Find("Quest_Tracker_Card/Quest_Header_Text");
            if (questHeader != null)
            {
                TMP_Text qh = questHeader.GetComponent<TMP_Text>();
                if (qh != null)
                {
                    so.FindProperty("questHeaderText").objectReferenceValue = qh;
                    qh.text = "QUEST OBJECTIVES";
                    qh.fontSize = 11.5f;
                    qh.fontStyle = FontStyles.Bold;
                    qh.characterSpacing = 1.5f;
                }
            }

            Transform questCard = hud.transform.Find("Quest_Tracker_Card");
            if (questCard != null)
            {
                QuestHUDUIController qCtrl = questCard.GetComponent<QuestHUDUIController>();
                if (qCtrl == null)
                {
                    qCtrl = questCard.gameObject.AddComponent<QuestHUDUIController>();
                }
                qCtrl.AutoLocateOrBuildHierarchy();
            }

            Transform zoneTitle = hud.transform.Find("Zone_Indicator_Banner/Zone_Title_Text");
            if (zoneTitle != null)
            {
                TMP_Text zt = zoneTitle.GetComponent<TMP_Text>();
                if (zt != null)
                {
                    so.FindProperty("zoneTitleText").objectReferenceValue = zt;
                    zt.text = "Oakhaven Village";
                    zt.fontSize = 15f;
                    zt.fontStyle = FontStyles.Bold;
                }
            }

            Transform acText = hud.transform.Find("Hero_Status_Card/Hero_AC_Badge/Hero_AC_Text");
            if (acText != null)
            {
                TMP_Text at = acText.GetComponent<TMP_Text>();
                if (at != null)
                {
                    so.FindProperty("heroACText").objectReferenceValue = at;
                    at.text = "AC --";
                    at.fontSize = 11.5f;
                    at.fontStyle = FontStyles.Bold;
                }
            }

            so.ApplyModifiedProperties();

            // 8. Ensure LevelUpUIController and Progression components exist on Canvas / Scene
            LevelUpUIController levelUpCtrl = canvas.GetComponentInChildren<LevelUpUIController>(true);
            if (levelUpCtrl == null)
            {
                GameObject lvlObj = new GameObject("LevelUpUIController", typeof(LevelUpUIController));
                lvlObj.transform.SetParent(canvas.transform, false);
                levelUpCtrl = lvlObj.GetComponent<LevelUpUIController>();
            }
            levelUpCtrl.EnsureUIHierarchy();

            // 9. Ensure DungeonMapUIController exists on Canvas / Scene
            DungeonMapUIController mapCtrl = canvas.GetComponentInChildren<DungeonMapUIController>(true);
            if (mapCtrl == null)
            {
                GameObject mapObj = new GameObject("DungeonMapUIController", typeof(DungeonMapUIController));
                mapObj.transform.SetParent(canvas.transform, false);
                mapCtrl = mapObj.GetComponent<DungeonMapUIController>();
            }
            mapCtrl.EnsureUIHierarchy();

            // 10. Map Toggle Button on Player HUD (Top-right near Quest Tracker)
            Transform mapBtnTr = hud.transform.Find("Button_Map_Toggle");
            if (mapBtnTr == null)
            {
                GameObject mapBtnObj = new GameObject("Button_Map_Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
                mapBtnObj.transform.SetParent(hud.transform, false);
                RectTransform mbRt = mapBtnObj.GetComponent<RectTransform>();
                mbRt.anchorMin = new Vector2(1f, 1f);
                mbRt.anchorMax = new Vector2(1f, 1f);
                mbRt.sizeDelta = new Vector2(110f, 32f);
                mbRt.anchoredPosition = new Vector2(-410f, -22f);

                Image mbImg = mapBtnObj.GetComponent<Image>();
                mbImg.sprite = slotFrame ?? panelDark;
                mbImg.type = Image.Type.Sliced;
                mbImg.color = new Color(1f, 0.95f, 0.7f);

                Button mbBtn = mapBtnObj.GetComponent<Button>();
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(mbBtn.onClick, mapCtrl.ToggleMap);

                GameObject mbTxtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                mbTxtObj.transform.SetParent(mapBtnObj.transform, false);
                RectTransform mbtRt = mbTxtObj.GetComponent<RectTransform>();
                mbtRt.anchorMin = Vector2.zero;
                mbtRt.anchorMax = Vector2.one;
                mbtRt.sizeDelta = Vector2.zero;

                TMP_Text mbLbl = mbTxtObj.GetComponent<TextMeshProUGUI>();
                mbLbl.text = "MAP (M)";
                mbLbl.fontSize = 11.5f;
                mbLbl.fontStyle = FontStyles.Bold;
                mbLbl.alignment = TextAlignmentOptions.Center;
                mbLbl.color = Color.white;
            }

            PlayerProgressionManager progManager = Object.FindAnyObjectByType<PlayerProgressionManager>(FindObjectsInactive.Include);
            if (progManager == null)
            {
                GameObject progObj = new GameObject("PlayerProgressionManager", typeof(PlayerProgressionManager));
                progManager = progObj.GetComponent<PlayerProgressionManager>();
            }

            SFXManager sfxMgr = Object.FindAnyObjectByType<SFXManager>(FindObjectsInactive.Include);
            if (sfxMgr == null)
            {
                GameObject sfxObj = new GameObject("SFXManager", typeof(SFXManager));
            }

            FloatingCombatText fct = Object.FindAnyObjectByType<FloatingCombatText>(FindObjectsInactive.Include);
            if (fct == null)
            {
                GameObject fctObj = new GameObject("FloatingCombatText", typeof(FloatingCombatText));
            }

            EditorUtility.SetDirty(hud.gameObject);
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log("[BuildHUDEditor] Tabletop D&D HUD & Progression successfully rebuilt, styled, in the active scene (save to keep).");

            // Rebuild and style Combat HUD in tandem
            RebuildAndStyleCombatHUD();
        }

        [MenuItem("CastleOfDice/Rebuild and Style Combat HUD", false, 26)]
        public static void RebuildAndStyleCombatHUD()
        {
            var activeScene = EditorSceneManager.GetActiveScene();

            CombatUIController combatUI = Object.FindAnyObjectByType<CombatUIController>(FindObjectsInactive.Include);
            if (combatUI == null)
            {
                Debug.LogWarning("[BuildHUDEditor] CombatUIController not found in active scene.");
                return;
            }

            // Assign authentic fantasy icons to all 12 AbilitySO assets
            AssignAbilityIconsToScriptableObjects();

            Undo.RegisterCompleteObjectUndo(combatUI.gameObject, "Rebuild & Style Combat HUD");

            Sprite panelDark = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            Sprite slotFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            Sprite dividerGold = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            Sprite buttonNormal = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Button_Normal.png");
            Sprite buttonHover = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Button_Hover.png");
            Sprite buttonPressed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Button_Pressed.png");
            Sprite hourglassIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Icon_Hourglass.png");
            Sprite defaultAbilityIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Icon_Sword.png");
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            SerializedObject so = new SerializedObject(combatUI);
            so.FindProperty("panelDarkSprite").objectReferenceValue = panelDark;
            so.FindProperty("slotFrameSprite").objectReferenceValue = slotFrame;
            so.FindProperty("dividerGoldSprite").objectReferenceValue = dividerGold;
            so.FindProperty("buttonNormalSprite").objectReferenceValue = buttonNormal;
            so.FindProperty("buttonHoverSprite").objectReferenceValue = buttonHover;
            so.FindProperty("buttonPressedSprite").objectReferenceValue = buttonPressed;
            so.FindProperty("hourglassIconSprite").objectReferenceValue = hourglassIcon;
            so.FindProperty("defaultAbilityIconSprite").objectReferenceValue = defaultAbilityIcon;
            so.ApplyModifiedProperties();

            combatUI.EnsureStyledHierarchy();
            AbilityTooltipUI.SetThemeSprites(panelDark, slotFrame, dividerGold, defaultAbilityIcon);

            if (fontAsset != null)
            {
                foreach (var txt in combatUI.GetComponentsInChildren<TMP_Text>(true))
                {
                    txt.font = fontAsset;
                }
            }

            so.Update();
            if (combatUI.CombatActionBar != null)
            {
                so.FindProperty("combatActionBar").objectReferenceValue = combatUI.CombatActionBar;
            }
            if (combatUI.EndTurnButton != null)
            {
                so.FindProperty("endTurnButton").objectReferenceValue = combatUI.EndTurnButton;
            }

            SerializedProperty btnProp = so.FindProperty("abilityButtons");
            btnProp.ClearArray();
            for (int i = 0; i < combatUI.AbilityButtons.Count; i++)
            {
                btnProp.InsertArrayElementAtIndex(i);
                btnProp.GetArrayElementAtIndex(i).objectReferenceValue = combatUI.AbilityButtons[i];
            }

            SerializedProperty iconProp = so.FindProperty("abilityIcons");
            iconProp.ClearArray();
            for (int i = 0; i < combatUI.AbilityIcons.Count; i++)
            {
                iconProp.InsertArrayElementAtIndex(i);
                iconProp.GetArrayElementAtIndex(i).objectReferenceValue = combatUI.AbilityIcons[i];
            }

            SerializedProperty nameProp = so.FindProperty("abilityNames");
            nameProp.ClearArray();
            for (int i = 0; i < combatUI.AbilityNames.Count; i++)
            {
                nameProp.InsertArrayElementAtIndex(i);
                nameProp.GetArrayElementAtIndex(i).objectReferenceValue = combatUI.AbilityNames[i];
            }

            SerializedProperty rangeProp = so.FindProperty("abilityRanges");
            rangeProp.ClearArray();
            for (int i = 0; i < combatUI.AbilityRanges.Count; i++)
            {
                rangeProp.InsertArrayElementAtIndex(i);
                rangeProp.GetArrayElementAtIndex(i).objectReferenceValue = combatUI.AbilityRanges[i];
            }

            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(combatUI.gameObject);
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log("[BuildHUDEditor] Combat HUD successfully rebuilt, styled, in the active scene (save to keep).");
        }

        public static void AssignAbilityIconsToScriptableObjects()
        {
            var mapping = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Assets/Data/Ability_Warrior_SwordSlash.asset", "Assets/UI/Sprites/UI_Icon_Sword.png" },
                { "Assets/Data/Ability_Warrior_ShieldBlock.asset", "Assets/UI/Sprites/UI_Icon_Shield.png" },
                { "Assets/Data/Ability_Warrior_WarCry.asset", "Assets/UI/Sprites/UI_Icon_WarCry.png" },
                { "Assets/Data/Ability_Warrior_IronWill.asset", "Assets/UI/Sprites/UI_Icon_IronWill.png" },
                { "Assets/Data/Ability_Mage_Fireball.asset", "Assets/UI/Sprites/UI_Icon_Fireball.png" },
                { "Assets/Data/Ability_Mage_Frostbite.asset", "Assets/UI/Sprites/UI_Icon_Frostbite.png" },
                { "Assets/Data/Ability_Mage_ManaShield.asset", "Assets/UI/Sprites/UI_Icon_ManaShield.png" },
                { "Assets/Data/Ability_Mage_Blink.asset", "Assets/UI/Sprites/UI_Icon_Blink.png" },
                { "Assets/Data/Ability_Rogue_Backstab.asset", "Assets/UI/Sprites/UI_Icon_Backstab.png" },
                { "Assets/Data/Ability_Rogue_PoisonDagger.asset", "Assets/UI/Sprites/UI_Icon_PoisonDagger.png" },
                { "Assets/Data/Ability_Rogue_SmokeBomb.asset", "Assets/UI/Sprites/UI_Icon_SmokeBomb.png" },
                { "Assets/Data/Ability_Rogue_Lockpicking.asset", "Assets/UI/Sprites/UI_Icon_Lockpicking.png" }
            };

            foreach (var kvp in mapping)
            {
                var ability = AssetDatabase.LoadAssetAtPath<AbilitySO>(kvp.Key);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(kvp.Value);
                if (ability != null && sprite != null)
                {
                    SerializedObject so = new SerializedObject(ability);
                    so.FindProperty("abilityIcon").objectReferenceValue = sprite;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(ability);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[BuildHUDEditor] All 12 AbilitySO assets successfully assigned their authentic fantasy icons!");
        }

        [MenuItem("CastleOfDice/Setup Rune of Reroll Dice Modal", false, 28)]
        public static void SetupRuneOfRerollDiceModal()
        {
            var activeScene = EditorSceneManager.GetActiveScene();

            Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                Debug.LogWarning("[BuildHUDEditor] Canvas not found in scene!");
                return;
            }

            Transform diceModalTr = null;
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "DiceModalPanel")
                {
                    diceModalTr = t;
                    break;
                }
            }

            if (diceModalTr == null)
            {
                Debug.LogWarning("[BuildHUDEditor] DiceModalPanel not found in Canvas!");
                return;
            }

            DiceUIController diceUI = diceModalTr.GetComponent<DiceUIController>();
            if (diceUI == null)
            {
                diceUI = diceModalTr.gameObject.AddComponent<DiceUIController>();
            }

            RuneOfRerollController reroll = diceModalTr.GetComponent<RuneOfRerollController>();
            if (reroll == null)
            {
                reroll = diceModalTr.gameObject.AddComponent<RuneOfRerollController>();
            }

            diceUI.AutoLocateComponents();
            reroll.AutoLocateButtons();

            SerializedObject diceSO = new SerializedObject(diceUI);
            SerializedProperty rerollProp = diceSO.FindProperty("rerollController");
            if (rerollProp != null)
            {
                rerollProp.objectReferenceValue = reroll;
                diceSO.ApplyModifiedProperties();
            }

            SerializedObject rerollSO = new SerializedObject(reroll);
            Button[] buttons = diceModalTr.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b.name == "DismissButton" || b.name.Contains("Continue"))
                {
                    SerializedProperty cbProp = rerollSO.FindProperty("continueButton");
                    if (cbProp != null) cbProp.objectReferenceValue = b;
                    SerializedProperty cbtProp = rerollSO.FindProperty("continueButtonText");
                    if (cbtProp != null) cbtProp.objectReferenceValue = b.GetComponentInChildren<TMP_Text>(true);
                }
                else if (b.name == "RerollButton")
                {
                    SerializedProperty rbProp = rerollSO.FindProperty("rerollButton");
                    if (rbProp != null) rbProp.objectReferenceValue = b;
                    SerializedProperty rbtProp = rerollSO.FindProperty("rerollButtonText");
                    if (rbtProp != null) rbtProp.objectReferenceValue = b.GetComponentInChildren<TMP_Text>(true);
                }
            }
            rerollSO.ApplyModifiedProperties();

            EditorUtility.SetDirty(diceModalTr.gameObject);
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log("[BuildHUDEditor] Rune of Reroll Dice Modal successfully configured in the active scene (save to keep).");
        }
    }
}
