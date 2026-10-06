using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// The D&D character sheet (C), Vili 2026-10-06: the six attributes with their scores, modifiers and saving
    /// throws, every skill (trained ones marked), attack bonus and save DC, the abilities with what they cost,
    /// worn magic items and features (Death's Door, Inspiration, the rogue's Expertise and Cunning Action).
    /// Built in code like the bag; it can be opened while exploring and in a fight.
    /// </summary>
    public class CharacterSheetUI : MonoBehaviour
    {
        public static CharacterSheetUI Instance { get; private set; }

        /// <summary>Key that opens and closes the sheet.</summary>
        public const KeyCode SheetKey = KeyCode.C;

        private const float PanelWidth = 1240f;
        private const float PanelHeight = 800f;

        private GameObject modalRoot;
        private TMP_Text titleText;
        private TMP_Text subtitleText;
        private TMP_Text statsText;
        private TMP_Text attributesText;
        private TMP_Text skillsText;
        private TMP_Text actionsText;
        private bool isBuilt;
        private int openedFrame;
        private int closedFrame = -1;
        private bool disabledExplorationInput;

        /// <summary>Whether the sheet is on screen.</summary>
        public static bool IsOpen => Instance != null && Instance.modalRoot != null && Instance.modalRoot.activeSelf;

        /// <summary>True on the frame the sheet closed with Esc (so Esc does not also open the pause menu).</summary>
        public static bool UsesEscape => IsOpen || (Instance != null && Instance.closedFrame == Time.frameCount);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnStart()
        {
            EnsureInstance();
        }

        /// <summary>The sheet's host object (created on demand, kept across scenes).</summary>
        public static CharacterSheetUI EnsureInstance()
        {
            if (Instance == null)
            {
                GameObject root = new GameObject("CharacterSheetUI");
                if (Application.isPlaying) DontDestroyOnLoad(root);
                Instance = root.AddComponent<CharacterSheetUI>();
            }
            Instance.BuildUI();
            return Instance;
        }

        /// <summary>Opens or closes the sheet.</summary>
        public static void Toggle()
        {
            CharacterSheetUI ui = EnsureInstance();
            if (IsOpen) ui.Close();
            else ui.Open();
        }

        public bool Open()
        {
            BuildUI();
            if (IsOpen) return true;
            PlayerUnit hero = FindAnyObjectByType<PlayerUnit>();
            if (hero == null || !CanOpenNow()) return false;

            Refresh(hero);
            modalRoot.SetActive(true);
            openedFrame = Time.frameCount;
            GameManager gm = GameManager.Instance;
            disabledExplorationInput = gm == null || gm.CurrentMode == GamePlayMode.Exploration;
            if (disabledExplorationInput) GameInput.SetExplorationInputEnabled(false);
            SFXManager.Instance?.PlaySFX(SFXClipType.ButtonClick, Vector3.zero);
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            closedFrame = Time.frameCount;
            modalRoot.SetActive(false);
            if (disabledExplorationInput) GameInput.SetExplorationInputEnabled(true);
            disabledExplorationInput = false;
        }

        private static bool CanOpenNow()
        {
            if (Dialogue.DialogueController.Instance != null && Dialogue.DialogueController.Instance.IsInDialogue) return false;
            if (StoryPanelUI.IsOpen || LockpickMinigameUI.IsOpen || TomeShelfMinigameUI.IsOpen || InventoryUI.IsOpen) return false;
            if (ShopUIController.Instance != null && ShopUIController.Instance.IsShopOpen) return false;
            if (DungeonMapUIController.Instance != null && DungeonMapUIController.Instance.IsMapOpen) return false;
            if (LevelUpUIController.Instance != null && LevelUpUIController.Instance.IsModalOpen) return false;
            if (MainMenuController.Instance != null && MainMenuController.Instance.IsMenuOpen) return false;
            return true;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (IsOpen && disabledExplorationInput) GameInput.SetExplorationInputEnabled(true);
            Instance = null;
        }

        private void Update()
        {
            if (IsOpen)
            {
                if (Time.frameCount == openedFrame) return;
                if (GameInput.GetKeyDown(SheetKey) || GameInput.GetKeyDown(KeyCode.Escape)) Close();
                return;
            }

            if (GameInput.GetKeyDown(SheetKey)) Open();
        }

        #region Content

        /// <summary>Fills the sheet from <paramref name="hero"/>.</summary>
        public void Refresh(PlayerUnit hero)
        {
            BuildUI();
            if (hero == null) return;

            CharacterClassType classType = hero.CharacterClass != null ? hero.CharacterClass.ClassType : CharacterClassType.Warrior;
            titleText.text = hero.UnitName;
            subtitleText.text = $"{classType}  •  Level {hero.Level}  •  Proficiency +{HeroAttributes.GetProficiencyBonus(hero)}";
            statsText.text = BuildStatsLine(hero);
            attributesText.text = BuildAttributesText(hero);
            skillsText.text = BuildSkillsText(hero);
            actionsText.text = BuildActionsText(hero);
        }

        /// <summary>HP, AC, speed, initiative, attack bonus and save DC in one line.</summary>
        public static string BuildStatsLine(PlayerUnit hero)
        {
            int dex = HeroAttributes.GetModifier(hero, HeroAttribute.Dexterity);
            return $"<color=#D1AD59>HP</color> {hero.CurrentHP}/{hero.MaxHP}     " +
                   $"<color=#D1AD59>AC</color> {hero.ArmorClass}     " +
                   $"<color=#D1AD59>Speed</color> {hero.MovementRange} tiles     " +
                   $"<color=#D1AD59>Initiative</color> {Signed(dex)}     " +
                   $"<color=#D1AD59>Attack</color> {Signed(HeroAttributes.GetAttackBonus(hero))}     " +
                   $"<color=#D1AD59>Save DC</color> {HeroAttributes.GetSaveDC(hero)}";
        }

        /// <summary>"STR 16 (+3)   Save +5 ●" for each attribute (● = proficient save).</summary>
        public static string BuildAttributesText(PlayerUnit hero)
        {
            StringBuilder sb = new StringBuilder(512);
            sb.Append("<color=#F6D578><b>ATTRIBUTES</b></color>\n<size=15><color=#8A93A3>score (modifier) · saving throw</color></size>\n\n");
            CharacterClassType classType = hero.CharacterClass != null ? hero.CharacterClass.ClassType : CharacterClassType.Warrior;
            HeroAttribute primary = HeroAttributes.GetPrimaryAttribute(classType);
            for (int i = 0; i < 6; i++)
            {
                HeroAttribute attribute = (HeroAttribute)i;
                int mod = HeroAttributes.GetModifier(hero, attribute);
                bool saveProf = HeroAttributes.IsSaveProficient(classType, attribute);
                string name = HeroAttributes.GetShortName(attribute);
                sb.Append(attribute == primary ? "<color=#F6D578>" : "<color=#FFF5D9>").Append("<b>").Append(name).Append("</b></color>  ")
                  .Append(HeroAttributes.GetScore(mod)).Append(" (").Append(Signed(mod)).Append(")")
                  .Append("     <color=#A4B7D1>Save ").Append(Signed(HeroAttributes.GetSaveBonus(hero, attribute))).Append("</color>")
                  .Append(saveProf ? " <color=#8FD18F>•</color>" : " <color=#4B5160>·</color>")
                  .Append('\n');
            }
            sb.Append("\n<size=15><color=#8A93A3>• proficient save   gold: your class's main attribute</color></size>");
            return sb.ToString();
        }

        /// <summary>Every skill with its bonus; trained skills are marked, Expertise twice.</summary>
        public static string BuildSkillsText(PlayerUnit hero)
        {
            StringBuilder sb = new StringBuilder(1024);
            sb.Append("<color=#F6D578><b>SKILLS</b></color>\n<size=15><color=#8A93A3>• trained (+proficiency)   •• expertise (x2)</color></size>\n\n");
            CharacterClassType classType = hero.CharacterClass != null ? hero.CharacterClass.ClassType : CharacterClassType.Warrior;
            for (int i = 0; i < HeroAttributes.SkillCount; i++)
            {
                HeroSkill skill = (HeroSkill)i;
                bool trained = HeroAttributes.IsProficient(classType, skill);
                bool expert = HeroAttributes.HasExpertise(classType, skill);
                string marker = expert ? "<color=#F6D578>••</color>" : trained ? "<color=#8FD18F>•</color>" : "<color=#4B5160>·</color>";
                sb.Append(marker).Append(' ')
                  .Append(trained ? "<color=#FFF5D9>" : "<color=#B8BCC6>").Append(HeroAttributes.GetSkillName(skill)).Append("</color>")
                  .Append(" <size=14><color=#8A93A3>(").Append(HeroAttributes.GetShortName(HeroAttributes.GetSkillAttribute(skill))).Append(")</color></size>  ")
                  .Append("<b>").Append(Signed(HeroAttributes.GetSkillBonus(hero, skill))).Append("</b>\n");
            }
            return sb.ToString();
        }

        /// <summary>Abilities with their cost, worn items and features.</summary>
        public static string BuildActionsText(PlayerUnit hero)
        {
            StringBuilder sb = new StringBuilder(1024);
            sb.Append("<color=#F6D578><b>ACTIONS</b></color>\n<size=15><color=#8A93A3>each turn: a move, an action, a bonus action; a reaction each round</color></size>\n\n");
            for (int i = 0; i < hero.ActiveAbilities.Count; i++)
            {
                AbilitySO ability = hero.ActiveAbilities[i];
                if (ability == null) continue;
                ActionCost cost = ActionEconomy.GetCost(ability);
                int cooldown = AbilityCooldowns.GetCooldownTurns(ability);
                sb.Append("<b>").Append(ability.AbilityName).Append("</b>  <color=")
                  .Append(cost == ActionCost.BonusAction ? "#7FD4FF" : "#8FD18F").Append('>').Append(ActionEconomy.CostLabel(cost)).Append("</color>");
                if (CombatRules.TryGetSave(ability, out HeroAttribute save, out _))
                {
                    sb.Append("  <size=15><color=#A4B7D1>").Append(HeroAttributes.GetShortName(save)).Append(" save</color></size>");
                }
                if (cooldown > 0) sb.Append("  <size=15><color=#8A93A3>cooldown ").Append(cooldown).Append("</color></size>");
                sb.Append('\n');
            }
            ActionCost mobility = hero.HasCunningAction ? ActionCost.BonusAction : ActionCost.Action;
            sb.Append("Dash, Disengage  <color=").Append(mobility == ActionCost.BonusAction ? "#7FD4FF" : "#8FD18F").Append('>')
              .Append(ActionEconomy.CostLabel(mobility)).Append("</color>\n");
            sb.Append("Dodge  <color=#8FD18F>Action</color>\n");
            sb.Append("Potion, Poison Blade  <color=#7FD4FF>Bonus action</color>\n");
            sb.Append(hero.ReactionWeapon != null
                ? "Opportunity attack  <color=#E8A07A>Reaction</color>\n"
                : "<color=#8A93A3>No opportunity attacks (no melee weapon)</color>\n");

            sb.Append("\n<color=#F6D578><b>WORN ITEMS</b></color>\n");
            bool any = false;
            for (int i = 0; i < MagicItems.AllIds.Length; i++)
            {
                string id = MagicItems.AllIds[i];
                if (!MagicItems.Owns(id)) continue;
                any = true;
                ItemSO item = InventoryManager.Instance != null ? InventoryManager.Instance.FindItemByID(id) : null;
                string name = item != null ? item.ItemName : id;
                bool worn = MagicItems.IsWorn(id);
                sb.Append(worn ? "<color=#FFF5D9>" : "<color=#6B7280>").Append(name).Append("</color>")
                  .Append("  <size=15><color=#A4B7D1>").Append(worn ? MagicItems.EffectText(id) : "taken off").Append("</color></size>\n");
            }
            if (!any) sb.Append("<color=#8A93A3>none yet</color>\n");

            sb.Append("\n<color=#F6D578><b>FEATURES</b></color>\n");
            sb.Append("Death's Door  <size=15><color=#A4B7D1>once per fight: DC 10 save to stay up at 0 HP</color></size>\n");
            sb.Append("Inspiration  ").Append(HeroicInspiration.Has ? "<color=#F6D578>ready (free reroll)</color>" : "<color=#8A93A3>none (earned by heroic deeds)</color>").Append('\n');
            if (hero.HasCunningAction)
            {
                sb.Append("Expertise, Cunning Action  <size=15><color=#A4B7D1>double proficiency in Stealth and Sleight of Hand</color></size>\n");
            }
            sb.Append("Sneak (V)  <size=15><color=#A4B7D1>Stealth check before a fight; success surprises the enemies</color></size>");
            return sb.ToString();
        }

        private static string Signed(int value) => value < 0 ? value.ToString() : "+" + value;

        #endregion

        #region UI Construction

        private void BuildUI()
        {
            if (isBuilt) return;
            isBuilt = true;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 92;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Full-screen dimmer; its "Modal" name keeps the world click raycaster from reaching through
            modalRoot = new GameObject("CharacterSheetModal", typeof(RectTransform));
            modalRoot.transform.SetParent(transform, false);
            RectTransform dimRect = (RectTransform)modalRoot.transform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            modalRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            RectTransform panel = Rect("CharacterSheetModal_Panel", modalRoot.transform, new Vector2(PanelWidth, PanelHeight), Vector2.zero);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            Sprite panelSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            if (panelSprite != null)
            {
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
                panelImage.color = Color.white;
            }
            else
            {
                panelImage.color = UITheme.PanelSlate;
            }

            titleText = Text("Title", panel, "Hero", 40f, UITheme.GoldAccent, new Vector2(0f, PanelHeight * 0.5f - 50f), new Vector2(900f, 52f));
            titleText.fontStyle = FontStyles.Bold;
            subtitleText = Text("Subtitle", panel, string.Empty, 21f, UITheme.CreamText, new Vector2(0f, PanelHeight * 0.5f - 92f), new Vector2(900f, 30f));
            statsText = Text("Stats", panel, string.Empty, 21f, UITheme.CreamText, new Vector2(0f, PanelHeight * 0.5f - 136f), new Vector2(PanelWidth - 80f, 32f));

            float columnTop = PanelHeight * 0.5f - 172f;
            float columnHeight = PanelHeight - 250f;
            attributesText = Column("Attributes", panel, new Vector2(-PanelWidth * 0.5f + 40f, columnTop), new Vector2(340f, columnHeight));
            skillsText = Column("Skills", panel, new Vector2(-PanelWidth * 0.5f + 410f, columnTop), new Vector2(330f, columnHeight));
            actionsText = Column("Actions", panel, new Vector2(-PanelWidth * 0.5f + 770f, columnTop), new Vector2(440f, columnHeight));

            // Close button, top-right
            RectTransform close = Rect("Close", panel, new Vector2(44f, 44f), new Vector2(PanelWidth * 0.5f - 40f, PanelHeight * 0.5f - 40f));
            Image closeImage = close.gameObject.AddComponent<Image>();
            closeImage.color = new Color(0.35f, 0.12f, 0.10f, 1f);
            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(Close);
            Text("X", close, "X", 24f, UITheme.CreamText, Vector2.zero, new Vector2(44f, 44f)).fontStyle = FontStyles.Bold;

            Text("Hint", panel, "[C] or [Esc]: close", 16f, UITheme.ParchmentText, new Vector2(0f, -PanelHeight * 0.5f + 26f), new Vector2(600f, 26f));

            modalRoot.SetActive(false);
        }

        private static TMP_Text Column(string name, Transform parent, Vector2 topLeft, Vector2 size)
        {
            TMP_Text text = Text(name, parent, string.Empty, 18f, UITheme.CreamText, Vector2.zero, size);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = 4f;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)obj.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static TMP_Text Text(string name, Transform parent, string text, float size, Color color, Vector2 position, Vector2 box)
        {
            RectTransform rect = Rect(name, parent, box, position);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.richText = true;
            return tmp;
        }

        #endregion
    }
}
