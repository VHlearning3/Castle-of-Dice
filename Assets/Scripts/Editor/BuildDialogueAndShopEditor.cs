using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using CastleOfTheD20.UI;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Editor
{
    /// <summary>
    /// Editor tool to build, style, and serialize the tabletop D&D NPC Dialogue Panels and
    /// Blacksmith Baldur's Shop interface in the active scene.
    /// Strictly adheres to the rule that authentic CoinIcon.png and HealthPotionIcon.png
    /// are preserved and wired with preserveAspect = true.
    /// </summary>
    public static class BuildDialogueAndShopEditor
    {
        [MenuItem("CastleOfDice/Rebuild and Style Dialogue and Shop Panels", false, 26)]
        public static void RebuildAndStyleDialogueAndShop()
        {
            var activeScene = EditorSceneManager.GetActiveScene();

            // 1. Ensure Theme Sprites exist
            Sprite panelDark = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            Sprite slotFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            Sprite dividerGold = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            Sprite pillBadge = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Pill_Badge.png");
            Sprite buttonNormal = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Button_Normal.png");
            Sprite buttonHover = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Button_Hover.png");
            Sprite buttonPressed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Button_Pressed.png");
            Sprite defaultPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Fantasy_Portrait_Placeholder.png");

            Sprite coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ICONSART/CoinIcon.png");
            Sprite potionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ICONSART/HealthPotionIcon.png");
            Sprite swordIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Icon_Sword.png");
            Sprite shieldIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Icon_Shield.png");
            Sprite scrapOreIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/UI_Icon_ScrapOre.png");

            GameObject optionButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/UI/OptionButtonPrefab.prefab");
            ItemSO potionItem = AssetDatabase.LoadAssetAtPath<ItemSO>("Assets/Data/Item_Potion_Health.asset");
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            // 2. Find Canvas
            Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                Debug.LogError("[BuildDialogueAndShopEditor] Canvas not found in scene!");
                return;
            }

            // 3. Setup Dialogue UI
            DialogueUIController dialogueUI = Object.FindAnyObjectByType<DialogueUIController>(FindObjectsInactive.Include);
            if (dialogueUI == null)
            {
                GameObject diagObj = GameObject.Find("DialoguePanel") ?? GameObject.Find("DialogueModal");
                if (diagObj == null)
                {
                    diagObj = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                    diagObj.transform.SetParent(canvas.transform, false);
                }
                dialogueUI = diagObj.GetComponent<DialogueUIController>() ?? diagObj.AddComponent<DialogueUIController>();
            }

            Undo.RegisterCompleteObjectUndo(dialogueUI.gameObject, "Rebuild & Style Dialogue Panel");

            SerializedObject diagSO = new SerializedObject(dialogueUI);
            diagSO.FindProperty("panelDarkSprite").objectReferenceValue = panelDark;
            diagSO.FindProperty("slotFrameSprite").objectReferenceValue = slotFrame;
            diagSO.FindProperty("dividerGoldSprite").objectReferenceValue = dividerGold;
            diagSO.FindProperty("buttonNormalSprite").objectReferenceValue = buttonNormal;
            diagSO.FindProperty("buttonHoverSprite").objectReferenceValue = buttonHover;
            diagSO.FindProperty("buttonPressedSprite").objectReferenceValue = buttonPressed;
            diagSO.FindProperty("defaultPortraitPlaceholder").objectReferenceValue = defaultPortrait;
            diagSO.FindProperty("optionButtonPrefab").objectReferenceValue = optionButtonPrefab;
            diagSO.ApplyModifiedProperties();

            dialogueUI.EnsureStyledHierarchy();
            dialogueUI.AutoLocateComponents();

            // Re-apply serialized properties to ensure all child references are bound
            diagSO.Update();
            Transform diagPanelTr = dialogueUI.transform.Find("DialoguePanel") ?? dialogueUI.transform;
            diagSO.FindProperty("dialoguePanel").objectReferenceValue = diagPanelTr.gameObject;
            
            Transform spkNameTr = diagPanelTr.Find("Dialogue_Content_Area/Speaker_Name_Text") ?? diagPanelTr.Find("NPC_Text_Area/Name");
            if (spkNameTr != null) diagSO.FindProperty("speakerNameText").objectReferenceValue = spkNameTr.GetComponent<TMP_Text>();

            Transform bodyTr = diagPanelTr.Find("Dialogue_Content_Area/Dialogue_Body_Text") ?? diagPanelTr.Find("NPC_Text_Area/Body");
            if (bodyTr != null) diagSO.FindProperty("dialogueBodyText").objectReferenceValue = bodyTr.GetComponent<TMP_Text>();

            Transform portTr = diagPanelTr.Find("Portrait_Slot_Frame/Speaker_Portrait");
            if (portTr != null) diagSO.FindProperty("speakerPortraitImage").objectReferenceValue = portTr.GetComponent<Image>();

            Transform portFrameTr = diagPanelTr.Find("Portrait_Slot_Frame");
            if (portFrameTr != null) diagSO.FindProperty("portraitFrameImage").objectReferenceValue = portFrameTr.GetComponent<Image>();

            Transform divTr = diagPanelTr.Find("Dialogue_Content_Area/Name_Divider");
            if (divTr != null) diagSO.FindProperty("nameDividerImage").objectReferenceValue = divTr.GetComponent<Image>();

            Transform contBtnTr = diagPanelTr.Find("ContinueButton") ?? diagPanelTr.Find("Continue_Button");
            if (contBtnTr != null) diagSO.FindProperty("continueButton").objectReferenceValue = contBtnTr.GetComponent<Button>();

            Transform optTr = dialogueUI.transform.Find("OptionsContainer") ?? diagPanelTr.Find("OptionsContainer");
            if (optTr != null) diagSO.FindProperty("optionsContainer").objectReferenceValue = optTr;

            diagSO.ApplyModifiedProperties();

            // 4. Setup Shop UI - Clean up duplicates under Canvas first
            ShopUIController[] existingShops = canvas.GetComponentsInChildren<ShopUIController>(true);
            ShopUIController shopUI = null;
            if (existingShops != null && existingShops.Length > 0)
            {
                // Prefer the one that has references or the first one
                for (int i = 0; i < existingShops.Length; i++)
                {
                    if (existingShops[i] != null && existingShops[i].gameObject != null)
                    {
                        if (shopUI == null)
                        {
                            shopUI = existingShops[i];
                        }
                        else
                        {
                            Undo.DestroyObjectImmediate(existingShops[i].gameObject);
                        }
                    }
                }
            }

            GameObject shopObj = shopUI != null ? shopUI.gameObject : null;
            if (shopObj == null)
            {
                shopObj = new GameObject("ShopPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                shopObj.transform.SetParent(canvas.transform, false);
                shopUI = shopObj.AddComponent<ShopUIController>();
            }

            Undo.RegisterCompleteObjectUndo(shopObj, "Rebuild & Style Baldur Shop");

            SerializedObject shopSO = new SerializedObject(shopUI);
            shopSO.FindProperty("shopPanel").objectReferenceValue = shopObj;
            shopSO.FindProperty("panelDarkSprite").objectReferenceValue = panelDark;
            shopSO.FindProperty("slotFrameSprite").objectReferenceValue = slotFrame;
            shopSO.FindProperty("dividerGoldSprite").objectReferenceValue = dividerGold;
            shopSO.FindProperty("pillBadgeSprite").objectReferenceValue = pillBadge;
            shopSO.FindProperty("buttonNormalSprite").objectReferenceValue = buttonNormal;
            shopSO.FindProperty("buttonHoverSprite").objectReferenceValue = buttonHover;
            shopSO.FindProperty("buttonPressedSprite").objectReferenceValue = buttonPressed;

            shopSO.FindProperty("coinSprite").objectReferenceValue = coinSprite;
            shopSO.FindProperty("potionSprite").objectReferenceValue = potionSprite;
            shopSO.FindProperty("swordIconSprite").objectReferenceValue = swordIcon;
            shopSO.FindProperty("shieldIconSprite").objectReferenceValue = shieldIcon;
            shopSO.FindProperty("scrapOreSprite").objectReferenceValue = scrapOreIcon;
            if (potionItem != null)
            {
                shopSO.FindProperty("healthPotionItem").objectReferenceValue = potionItem;
            }
            shopSO.ApplyModifiedProperties();

            shopUI.EnsureStyledHierarchy();
            shopUI.AutoLocateComponents();

            // Re-apply serialized properties to ensure all child references are bound
            shopSO.Update();

            Transform goldTextTr = shopObj.transform.Find("Currency_Bar/Gold_Pill/Gold_Balance_Text") 
                ?? shopObj.transform.Find("Currency_Bar/Gold_Pill/Gold_Text")
                ?? shopObj.transform.Find("CurrencyContainer/GoldText");
            if (goldTextTr != null) shopSO.FindProperty("goldBalanceText").objectReferenceValue = goldTextTr.GetComponent<TMP_Text>();

            Transform scrapTextTr = shopObj.transform.Find("Currency_Bar/Scrap_Pill/Scrap_Metal_Text")
                ?? shopObj.transform.Find("Currency_Bar/Scrap_Pill/Scrap_Text")
                ?? shopObj.transform.Find("CurrencyContainer/ScrapText");
            if (scrapTextTr != null) shopSO.FindProperty("scrapMetalText").objectReferenceValue = scrapTextTr.GetComponent<TMP_Text>();

            Transform coinIconTr = shopObj.transform.Find("Currency_Bar/Gold_Pill/Gold_Icon");
            if (coinIconTr != null) shopSO.FindProperty("coinIconImage").objectReferenceValue = coinIconTr.GetComponent<Image>();

            Transform scrapIconTr = shopObj.transform.Find("Currency_Bar/Scrap_Pill/Scrap_Icon");
            if (scrapIconTr != null) shopSO.FindProperty("scrapIconImage").objectReferenceValue = scrapIconTr.GetComponent<Image>();

            Transform potionIconTr = shopObj.transform.Find("Stock_Shelf_Container/Row_Potion/Item_Slot_Frame/Item_Icon");
            if (potionIconTr != null) shopSO.FindProperty("potionIconImage").objectReferenceValue = potionIconTr.GetComponent<Image>();

            Transform swordIconTr = shopObj.transform.Find("Stock_Shelf_Container/Row_Sword/Item_Slot_Frame/Item_Icon");
            if (swordIconTr != null) shopSO.FindProperty("weaponIconImage").objectReferenceValue = swordIconTr.GetComponent<Image>();

            Transform shieldIconTr = shopObj.transform.Find("Stock_Shelf_Container/Row_Shield/Item_Slot_Frame/Item_Icon");
            if (shieldIconTr != null) shopSO.FindProperty("armorIconImage").objectReferenceValue = shieldIconTr.GetComponent<Image>();

            Transform scrapActIconTr = shopObj.transform.Find("Stock_Shelf_Container/Row_Scrap/Item_Slot_Frame/Item_Icon");
            if (scrapActIconTr != null) shopSO.FindProperty("scrapActionIconImage").objectReferenceValue = scrapActIconTr.GetComponent<Image>();

            Transform buyPotionBtnTr = shopObj.transform.Find("Stock_Shelf_Container/Row_Potion/BuyPotionButton")
                ?? shopObj.transform.Find("Stock_Shelf_Container/Row_Potion/Action_Button")
                ?? shopObj.transform.Find("ActionContainer/BuyPotionButton");
            if (buyPotionBtnTr != null) shopSO.FindProperty("buyPotionButton").objectReferenceValue = buyPotionBtnTr.GetComponent<Button>();

            Transform buyWeaponBtnTr = shopObj.transform.Find("Stock_Shelf_Container/Row_Sword/BuyWeaponButton")
                ?? shopObj.transform.Find("Stock_Shelf_Container/Row_Sword/Action_Button")
                ?? shopObj.transform.Find("ActionContainer/BuyWeaponButton");
            if (buyWeaponBtnTr != null) shopSO.FindProperty("buyWeaponButton").objectReferenceValue = buyWeaponBtnTr.GetComponent<Button>();

            Transform buyArmorBtnTr = shopObj.transform.Find("Stock_Shelf_Container/Row_Shield/BuyArmorButton")
                ?? shopObj.transform.Find("Stock_Shelf_Container/Row_Shield/Action_Button")
                ?? shopObj.transform.Find("ActionContainer/BuyArmorButton");
            if (buyArmorBtnTr != null) shopSO.FindProperty("buyArmorButton").objectReferenceValue = buyArmorBtnTr.GetComponent<Button>();

            Transform sellScrapBtnTr = shopObj.transform.Find("Stock_Shelf_Container/Row_Scrap/SellAllScrapButton")
                ?? shopObj.transform.Find("Stock_Shelf_Container/Row_Scrap/Action_Button")
                ?? shopObj.transform.Find("ActionContainer/SellAllScrapButton");
            if (sellScrapBtnTr != null)
            {
                Button ssb = sellScrapBtnTr.GetComponent<Button>();
                shopSO.FindProperty("sellAllScrapButton").objectReferenceValue = ssb;
                TMP_Text ssbl = sellScrapBtnTr.GetComponentInChildren<TMP_Text>(true);
                if (ssbl != null) shopSO.FindProperty("sellScrapButtonLabel").objectReferenceValue = ssbl;
            }

            Transform exitBtnTr = shopObj.transform.Find("ExitShopButton")
                ?? shopObj.transform.Find("LeaveShopButton")
                ?? shopObj.transform.Find("CloseButton");
            if (exitBtnTr != null) shopSO.FindProperty("exitShopButton").objectReferenceValue = exitBtnTr.GetComponent<Button>();

            Transform cornerCloseTr = shopObj.transform.Find("Close_Corner_Button");
            if (cornerCloseTr != null) shopSO.FindProperty("closeCornerButton").objectReferenceValue = cornerCloseTr.GetComponent<Button>();

            shopSO.ApplyModifiedProperties();

            // 5. Ensure fonts across all texts in Dialogue and Shop panels
            TMP_Text[] allTexts = dialogueUI.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in allTexts)
            {
                if (fontAsset != null) t.font = fontAsset;
                t.raycastTarget = false;
            }

            TMP_Text[] shopTexts = shopObj.GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in shopTexts)
            {
                if (fontAsset != null) t.font = fontAsset;
                t.raycastTarget = false;
            }

            // Ensure start inactive so they don't cover initial game view
            dialogueUI.gameObject.SetActive(false);
            shopObj.SetActive(false);

            EditorUtility.SetDirty(dialogueUI.gameObject);
            EditorUtility.SetDirty(shopObj);
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log("[BuildDialogueAndShopEditor] Successfully rebuilt, styled Dialogue and Baldur Shop panels in the active scene (save to keep).");
        }
    }
}
