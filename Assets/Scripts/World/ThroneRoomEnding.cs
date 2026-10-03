using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Bosses;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// The game's ending, played in the Throne Room once the Gargoyle King falls: the petrification breaks
    /// and the castle wakes (the stone knights regain their colour, the lava cracks close, the light turns
    /// warm), Elder Othelia appears at the hall door for a short closing conversation, a stats screen sums
    /// up the adventure (turns, natural 20s, gold, falls), the credits roll and the game returns to the
    /// main menu in Oakhaven.
    /// </summary>
    public class ThroneRoomEnding : MonoBehaviour
    {
        public const string OtheliaName = "Elder Othelia";
        public const string MainMenuSceneName = VillageNPC.StartingVillageSceneName;
        public const string PetrifiedKnightsName = "Petrified_Knights";

        #region Serialized Fields

        [Header("Scene References")]
        [Tooltip("Othelia, hidden at the Great Hall door until the curse breaks.")]
        [SerializeField] private GameObject othelia;

        [Tooltip("Root of the petrified knight statues that come back to life.")]
        [SerializeField] private Transform petrifiedKnights;

        [Tooltip("The phase-2 lava cracks, closed again when the curse breaks.")]
        [SerializeField] private LavaCracksReveal lavaCracks;

        [Tooltip("Light warmed to gold as the castle wakes (optional).")]
        [SerializeField] private Light warmLight;

        [Tooltip("Metres in front of Othelia where the hero waits for her.")]
        [SerializeField] private float meetingDistance = 3.5f;

        [Header("Timing")]
        [Tooltip("Seconds the victory moment plays before the curse breaks.")]
        [SerializeField] private float delayAfterVictory = 2.5f;

        [Tooltip("Seconds the credits take to scroll before the game returns to the main menu.")]
        [SerializeField] private float creditsDuration = 22f;

        #endregion

        #region Private State

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color AwakenedArmor = new Color(0.60f, 0.66f, 0.78f, 1f);
        private static readonly Color WarmGold = new Color(1f, 0.82f, 0.55f, 1f);

        private bool hasStarted;
        private bool dialogueFinished;
        private bool continuePressed;
        private Canvas overlayCanvas;
        private Image flashImage;
        private TMP_Text captionText;

        #endregion

        #region Public Properties

        /// <summary>Whether the ending sequence has begun.</summary>
        public bool HasStarted => hasStarted;

        /// <summary>Othelia's stand-in at the hall door.</summary>
        public GameObject Othelia => othelia;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (othelia != null) othelia.SetActive(false);

            // Re-dressing the zone rebuilds the statues; find them again by name
            if (petrifiedKnights == null)
            {
                GameObject knights = GameObject.Find(PetrifiedKnightsName);
                if (knights != null) petrifiedKnights = knights.transform;
            }
        }

        private void OnEnable() => GameManager.OnGameWon += HandleGameWon;
        private void OnDisable() => GameManager.OnGameWon -= HandleGameWon;

        #endregion

        #region Sequence

        private void HandleGameWon()
        {
            if (hasStarted) return;
            hasStarted = true;
            StartCoroutine(PlayEnding());
        }

        private IEnumerator PlayEnding()
        {
            yield return new WaitForSecondsRealtime(delayAfterVictory);

            // Let a finishing blow's walk and death clip settle before taking control away
            float settle = 0f;
            while (TurnManager.Instance != null && TurnManager.Instance.IsCombatActive && settle < 3f)
            {
                settle += Time.unscaledDeltaTime;
                yield return null;
            }

            GameInput.SetExplorationInputEnabled(false);
            if (DungeonMapUIController.Instance != null && DungeonMapUIController.Instance.IsMapOpen)
            {
                DungeonMapUIController.Instance.CloseMap();
                GameInput.SetExplorationInputEnabled(false);
            }

            BuildOverlay();
            PlayerUnit hero = FindAnyObjectByType<PlayerUnit>();

            // 1. The curse breaks: a white flash, a tremor and the stone giving way
            PlaySfx(SFXClipType.CriticalSuccess);
            AbilityVfx.PlayCameraShake(0.6f, 0.25f);
            yield return Fade(flashImage, 0f, 1f, 0.35f);
            StageMeeting(hero);
            BreakPetrification();
            ShowCaption("The petrification shatters.\nAcross the castle, stone turns back to flesh and the halls wake.");
            yield return Fade(flashImage, 1f, 0f, 1.4f);
            yield return WakeKnights(2.5f);
            yield return new WaitForSecondsRealtime(1.0f);
            ShowCaption(string.Empty);

            // 2. Othelia appears at the hall door (she has no walk clip, so she steps out of the light)
            yield return OtheliaArrives(hero);

            // 3. The closing conversation
            dialogueFinished = false;
            DialogueController.OnDialogueEnded += HandleDialogueEnded;
            DialogueController dialogue = DialogueController.Instance;
            dialogue.StartDialogue(BuildEndingDialogue(hero), hero);
            while (!dialogueFinished && dialogue.IsInDialogue) yield return null;
            DialogueController.OnDialogueEnded -= HandleDialogueEnded;
            GameInput.SetExplorationInputEnabled(false);

            // 4. Stats, then credits
            yield return ShowStats(hero);
            yield return RollCredits();

            // 5. Back to the main menu in Oakhaven
            ReturnToMainMenu();
        }

        private void HandleDialogueEnded()
        {
            dialogueFinished = true;
        }

        /// <summary>
        /// Behind the white flash, brings the hero to the Great Hall door where Othelia will appear, with the
        /// camera behind the hero looking at the door.
        /// </summary>
        private void StageMeeting(PlayerUnit hero)
        {
            if (hero == null || othelia == null) return;

            Vector3 spot = othelia.transform.position + othelia.transform.forward * meetingDistance;
            spot.y = hero.transform.position.y;

            CharacterController cc = hero.GetComponent<CharacterController>();
            bool ccWasEnabled = cc != null && cc.enabled;
            if (ccWasEnabled) cc.enabled = false;
            hero.ClearTile();
            hero.transform.position = spot;
            hero.FaceTowards(othelia.transform.position);
            Physics.SyncTransforms();
            if (ccWasEnabled) cc.enabled = true;

            CameraFollow follow = FindAnyObjectByType<CameraFollow>();
            if (follow != null)
            {
                follow.SetTarget(hero.transform, true);
                follow.FaceDirection(othelia.transform.position - spot);
            }
        }

        private void BreakPetrification()
        {
            if (lavaCracks == null) lavaCracks = FindAnyObjectByType<LavaCracksReveal>();
            if (lavaCracks != null) lavaCracks.Restore();

            if (warmLight != null)
            {
                warmLight.color = WarmGold;
                warmLight.intensity = Mathf.Max(warmLight.intensity, 2.2f);
            }
        }

        /// <summary>Tints the knights' grey statue stone into steel armour over <paramref name="duration"/> seconds.</summary>
        private IEnumerator WakeKnights(float duration)
        {
            if (petrifiedKnights == null) yield break;

            var targets = new List<(Renderer renderer, int index, Color from)>();
            Renderer[] renderers = petrifiedKnights.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] mats = renderers[r].sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || m.name.IndexOf("StoneStatue", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Color from = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : (m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.grey);
                    targets.Add((renderers[r], i, from));
                }
            }

            var block = new MaterialPropertyBlock();
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                ApplyKnightTint(targets, block, t / duration);
                yield return null;
            }
            ApplyKnightTint(targets, block, 1f);
        }

        private static void ApplyKnightTint(List<(Renderer renderer, int index, Color from)> targets, MaterialPropertyBlock block, float k)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var (renderer, index, from) = targets[i];
                if (renderer == null) continue;
                Color c = Color.Lerp(from, AwakenedArmor, Mathf.SmoothStep(0f, 1f, k));
                renderer.GetPropertyBlock(block, index);
                block.SetColor(BaseColorId, c);
                block.SetColor(ColorId, c);
                renderer.SetPropertyBlock(block, index);
            }
        }

        private IEnumerator OtheliaArrives(PlayerUnit hero)
        {
            if (othelia == null) yield break;

            if (hero != null)
            {
                Vector3 look = hero.transform.position - othelia.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) othelia.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            // A soft gold glow at the door, then she is there
            GameObject glowObj = new GameObject("Othelia_Arrival_Glow");
            glowObj.transform.position = othelia.transform.position + Vector3.up * 2f;
            Light glow = glowObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = WarmGold;
            glow.range = 8f;

            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                glow.intensity = Mathf.Lerp(0f, 6f, t / 0.6f);
                yield return null;
            }

            othelia.SetActive(true);
            VillageNPC npc = othelia.GetComponent<VillageNPC>();
            if (npc != null) npc.IsInteractable = false;
            PlaySfx(SFXClipType.ButtonClick);

            if (hero != null) hero.FaceTowards(othelia.transform.position);

            for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
            {
                glow.intensity = Mathf.Lerp(6f, 0f, t / 1.2f);
                yield return null;
            }
            Destroy(glowObj);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        /// <summary>The closing conversation with Othelia, built at runtime so it can name the hero.</summary>
        public static DialogueNodeSO BuildEndingDialogue(PlayerUnit hero)
        {
            string heroName = hero != null && !string.IsNullOrEmpty(hero.UnitName) ? hero.UnitName : "hero";

            DialogueNodeSO third = ScriptableObject.CreateInstance<DialogueNodeSO>();
            third.Initialize(OtheliaName,
                $"Ours. Oakhaven's. And yours, {heroName}. Come, the Great Hall is waking, and every soul in it wants to thank the one who rolled the dice for them.");
            third.SetOptions(new List<DialogueOption> { new DialogueOption("[End] Lead the way.", null) });

            DialogueNodeSO second = ScriptableObject.CreateInstance<DialogueNodeSO>();
            second.Initialize(OtheliaName,
                "He ruled these lands justly once, before he sought to live forever. Thanks to you, that is how he will be remembered, not for the shadows.");
            second.SetOptions(new List<DialogueOption> { new DialogueOption("The castle is yours again, Elder.", third) });

            DialogueNodeSO first = ScriptableObject.CreateInstance<DialogueNodeSO>();
            first.Initialize(OtheliaName,
                "I felt it all the way in Oakhaven: the stone let go of the castle. The King's curse is broken, and his knights are breathing again!");
            first.SetOptions(new List<DialogueOption> { new DialogueOption("The Petrified King can finally rest.", second) });

            return first;
        }

        #endregion

        #region Stats & Credits

        /// <summary>Lines of the stats screen for the adventure that just ended.</summary>
        public static string BuildStatsText(PlayerUnit hero)
        {
            string heroName = hero != null && !string.IsNullOrEmpty(hero.UnitName) ? hero.UnitName : "The hero";
            int gold = InventoryManager.Instance != null ? InventoryManager.Instance.CurrentGold : 0;
            return $"<b>{heroName}</b> broke the curse of the Castle of Dice\n\n" +
                   $"Combat turns taken:   <color=#ffd34d><b>{AdventureStats.TurnsTaken}</b></color>\n" +
                   $"Natural 20s rolled:   <color=#ffd34d><b>{AdventureStats.NaturalTwenties}</b></color>\n" +
                   $"Gold in the purse:   <color=#ffd34d><b>{gold}</b></color>\n" +
                   $"Times fallen:   <color=#ffd34d><b>{AdventureStats.Deaths}</b></color>";
        }

        public const string CreditsText =
            "<size=64><b><color=#ffd34d>CASTLE OF DICE</color></b></size>\n\n\n" +
            "<color=#ffd34d>Game design & development</color>\nVili\n\n" +
            "<color=#ffd34d>Heroes</color>\nSir Roland the Warrior\nScholar Elira the Mage\nShadow-Corvo the Rogue\n\n" +
            "<color=#ffd34d>Villains</color>\nThe Cursed Commander\nShadow Mage Malakor\nThe Gargoyle King\n\n" +
            "<color=#ffd34d>Music</color>\nScottish Harp (Pixabay)\n\n" +
            "<color=#ffd34d>Made with</color>\nUnity 6\n\n\n\n" +
            "<size=48><b>Thank you for playing!</b></size>";

        private IEnumerator ShowStats(PlayerUnit hero)
        {
            GameObject panel = CreatePanel("Ending_Stats", new Color(0.03f, 0.03f, 0.06f, 0.92f));
            CanvasGroup group = panel.AddComponent<CanvasGroup>();

            TMP_Text title = CreateText(panel.transform, "Title", "THE CURSE IS BROKEN", 60f, UITheme.GoldAccent, FontStyles.Bold);
            title.rectTransform.anchoredPosition = new Vector2(0f, 300f);
            title.rectTransform.sizeDelta = new Vector2(1400f, 90f);

            TMP_Text stats = CreateText(panel.transform, "Stats", BuildStatsText(hero), 36f, UITheme.CreamText, FontStyles.Normal);
            stats.rectTransform.anchoredPosition = new Vector2(0f, 20f);
            stats.rectTransform.sizeDelta = new Vector2(1200f, 420f);
            stats.lineSpacing = 18f;

            continuePressed = false;
            Button button = UIFactory.CreateTextButton(panel.transform, "Continue_Btn", "Continue", new Vector2(0f, -320f), new Vector2(260f, 56f), UITheme.ActionGreen);
            button.onClick.AddListener(() => continuePressed = true);

            yield return Fade(group, 0f, 1f, 0.8f);
            while (!continuePressed) yield return null;
            PlaySfx(SFXClipType.ButtonClick);
            yield return Fade(group, 1f, 0f, 0.5f);
            Destroy(panel);
        }

        private IEnumerator RollCredits()
        {
            GameObject panel = CreatePanel("Ending_Credits", Color.black);
            panel.AddComponent<RectMask2D>();

            TMP_Text text = CreateText(panel.transform, "Credits", CreditsText, 34f, UITheme.CreamText, FontStyles.Normal);
            RectTransform rt = text.rectTransform;
            rt.sizeDelta = new Vector2(1400f, 1700f);
            const float startY = -1400f;
            const float endY = 1400f;
            rt.anchoredPosition = new Vector2(0f, startY);

            TMP_Text hint = CreateText(panel.transform, "Skip_Hint", "Click to skip", 20f, new Color(1f, 1f, 1f, 0.45f), FontStyles.Italic);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(-140f, 40f);
            hint.rectTransform.sizeDelta = new Vector2(240f, 40f);

            float duration = Mathf.Max(1f, creditsDuration);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, t / duration));
                if (t > 1f && GameInput.GetLeftMouseButtonDown()) break;
                yield return null;
            }
        }

        private static void ReturnToMainMenu()
        {
            GameInput.SetExplorationInputEnabled(true);
            if (GameManager.Instance != null) GameManager.Instance.SetMode(GamePlayMode.Exploration);

            if (SceneLoader.Instance != null && Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            {
                SceneLoader.Instance.LoadSceneAsync(MainMenuSceneName, ShowMainMenu, "StartSpawn");
            }
            else
            {
                ShowMainMenu();
            }
        }

        private static void ShowMainMenu()
        {
            MainMenuController menu = MainMenuController.Instance ?? FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (menu != null) menu.ShowMainMenu();
        }

        #endregion

        #region Overlay UI

        private void BuildOverlay()
        {
            if (overlayCanvas != null) return;

            GameObject root = new GameObject("Ending_Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            overlayCanvas = root.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 1000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            MainMenuController.EnsureEventSystem();

            GameObject flash = CreatePanel("Curse_Flash", Color.white);
            flashImage = flash.GetComponent<Image>();
            flashImage.raycastTarget = false;
            SetAlpha(flashImage, 0f);

            captionText = CreateText(overlayCanvas.transform, "Caption", string.Empty, 34f, UITheme.CreamText, FontStyles.Italic);
            captionText.rectTransform.anchorMin = captionText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            captionText.rectTransform.anchoredPosition = new Vector2(0f, 170f);
            captionText.rectTransform.sizeDelta = new Vector2(1500f, 140f);
            captionText.outlineWidth = 0.2f;
            captionText.outlineColor = Color.black;
        }

        private void ShowCaption(string text)
        {
            if (captionText != null) captionText.text = text;
        }

        private GameObject CreatePanel(string name, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlayCanvas.transform, false);
            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static TMP_Text CreateText(Transform parent, string name, string content, float size, Color color, FontStyles style)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TMP_Text text = obj.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void SetAlpha(Graphic graphic, float a)
        {
            Color c = graphic.color;
            c.a = a;
            graphic.color = c;
        }

        private static IEnumerator Fade(Graphic graphic, float from, float to, float duration)
        {
            if (graphic == null) yield break;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(graphic, Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetAlpha(graphic, to);
        }

        private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }

        private static void PlaySfx(SFXClipType clip)
        {
            if (SFXManager.Instance != null) SFXManager.Instance.PlaySFX(clip);
        }

        #endregion

        #region Editor Wiring

        /// <summary>Wires the scene references (used by the Throne Room setup tool).</summary>
        public void Configure(GameObject otheliaStandIn, Transform knights, LavaCracksReveal cracks, Light light)
        {
            othelia = otheliaStandIn;
            petrifiedKnights = knights;
            lavaCracks = cracks;
            warmLight = light;
        }

        #endregion
    }
}
