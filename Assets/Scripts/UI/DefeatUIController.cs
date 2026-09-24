using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.World;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Defeat Screen Controller. Displays a dramatic game over modal upon party defeat
    /// with options to retry the battle or retreat to Oakhaven Village.
    /// Fulfills the "Defeat screen" requirement from the notebook.
    /// </summary>
    public class DefeatUIController : MonoBehaviour
    {
        #region Singleton

        public static DefeatUIController Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("UI Panels")]
        [Tooltip("Root defeat screen canvas or modal container.")]
        [SerializeField] private GameObject defeatModalPanel;

        [Header("Text Labels")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;

        [Header("Action Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button returnToVillageButton;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureUIHierarchy();
        }

        private void OnEnable()
        {
            TurnManager.OnTurnStateChanged += HandleTurnStateChanged;

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(OnRetryClicked);
            }
            if (returnToVillageButton != null)
            {
                returnToVillageButton.onClick.AddListener(OnReturnToVillageClicked);
            }
        }

        private void OnDisable()
        {
            TurnManager.OnTurnStateChanged -= HandleTurnStateChanged;

            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(OnRetryClicked);
            }
            if (returnToVillageButton != null)
            {
                returnToVillageButton.onClick.RemoveListener(OnReturnToVillageClicked);
            }
        }

        private void Start()
        {
            Hide();
        }

        #endregion

        #region Show / Hide

        public void Show(string customMessage = null)
        {
            EnsureUIHierarchy();

            if (defeatModalPanel != null)
            {
                defeatModalPanel.SetActive(true);
            }

            if (titleText != null)
            {
                titleText.text = "SANKARISI KAATUI";
            }

            if (messageText != null)
            {
                messageText.text = string.IsNullOrEmpty(customMessage)
                    ? "Linnan varjot olivat tällä kertaa liikaa...\nMutta noppa voi vielä kääntyä eduksesi."
                    : customMessage;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundType.Defeat);
            }
        }

        public void Hide()
        {
            if (defeatModalPanel != null)
            {
                defeatModalPanel.SetActive(false);
            }
        }

        #endregion

        #region Event Callbacks

        private void HandleTurnStateChanged(TurnState newState)
        {
            if (newState == TurnState.Defeat)
            {
                Show();
            }
        }

        private void OnRetryClicked()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundType.ButtonClick);
            }

            Hide();

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player != null)
            {
                player.Heal(player.MaxHP);
                player.HasActedThisTurn = false;
                player.HasMovedThisTurn = false;
            }

            // Restart encounter if a DungeonRoomController is present in the active zone
            DungeonRoomController room = FindAnyObjectByType<DungeonRoomController>();
            if (room != null && TurnManager.Instance != null)
            {
                // Reset enemy health
                if (room.roomEnemies != null)
                {
                    foreach (var enemyObj in room.roomEnemies)
                    {
                        if (enemyObj != null)
                        {
                            enemyObj.SetActive(true);
                            EnemyUnit enemy = enemyObj.GetComponent<EnemyUnit>();
                            if (enemy != null)
                            {
                                enemy.Heal(enemy.MaxHP);
                            }
                        }
                    }
                }

                TurnManager.Instance.StartCombatEncounter(player, room.roomLocation, room.bossIdentifier);
            }
            else if (TurnManager.Instance != null)
            {
                TurnManager.Instance.EndCombat(false);
            }
        }

        private void OnReturnToVillageClicked()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundType.ButtonClick);
            }

            Hide();

            // End combat mode and restore player health
            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player != null)
            {
                player.Heal(player.MaxHP);
            }

            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.EndCombat(false);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetLocation(GameLocation.Village);
                GameManager.Instance.SetPlayMode(GamePlayMode.Exploration);
            }

            // Teleport player back to village start point
            GameObject spawnPoint = GameObject.Find("StartSpawn") ?? GameObject.Find("Village_PlayerExitPoint");
            if (spawnPoint != null && player != null)
            {
                player.transform.position = spawnPoint.transform.position + Vector3.up * 0.2f;
                player.transform.rotation = spawnPoint.transform.rotation;
            }
        }

        #endregion

        #region Hierarchy Construction Fallback

        private void EnsureUIHierarchy()
        {
            if (defeatModalPanel != null) return;

            Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            // Check if already in canvas
            Transform existing = canvas.transform.Find("DefeatModalPanel");
            if (existing != null)
            {
                defeatModalPanel = existing.gameObject;
                return;
            }

            GameObject panel = new GameObject("DefeatModalPanel");
            panel.transform.SetParent(canvas.transform, false);
            defeatModalPanel = panel;

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            Image backdrop = panel.AddComponent<Image>();
            backdrop.color = new Color(0.12f, 0.02f, 0.02f, 0.88f); // Deep ominous red tint

            // Modal Card Box
            GameObject card = new GameObject("Defeat_Card");
            card.transform.SetParent(panel.transform, false);
            RectTransform cardRect = card.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(480f, 320f);
            cardRect.anchoredPosition = Vector2.zero;

            Image cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.15f, 0.15f, 0.18f, 0.98f);

            Outline cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            // Title
            GameObject titleObj = new GameObject("Title_Text");
            titleObj.transform.SetParent(card.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 95f);
            titleRect.sizeDelta = new Vector2(440f, 50f);
            titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "SANKARISI KAATUI";
            titleText.fontSize = 32f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(1f, 0.3f, 0.3f);

            // Message
            GameObject msgObj = new GameObject("Message_Text");
            msgObj.transform.SetParent(card.transform, false);
            RectTransform msgRect = msgObj.AddComponent<RectTransform>();
            msgRect.anchoredPosition = new Vector2(0f, 25f);
            msgRect.sizeDelta = new Vector2(420f, 70f);
            messageText = msgObj.AddComponent<TextMeshProUGUI>();
            messageText.text = "Linnan varjot olivat tällä kertaa liikaa...\nMutta noppa voi vielä kääntyä eduksesi.";
            messageText.fontSize = 16f;
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.color = new Color(0.85f, 0.85f, 0.85f);

            // Retry Button
            retryButton = CreateModalButton(card.transform, "Retry_Button", "Yritä uudelleen", new Vector2(0f, -45f), new Color(0.2f, 0.5f, 0.25f));

            // Return to Village Button
            returnToVillageButton = CreateModalButton(card.transform, "ReturnVillage_Button", "Palaa Kivenkoloon", new Vector2(0f, -105f), new Color(0.3f, 0.3f, 0.4f));

            panel.SetActive(false);
        }

        private Button CreateModalButton(Transform parent, string name, string label, Vector2 pos, Color normalColor)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 44f);
            rect.anchoredPosition = pos;

            Image img = btnObj.AddComponent<Image>();
            img.color = normalColor;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = normalColor * 1.25f;
            cb.pressedColor = normalColor * 0.85f;
            btn.colors = cb;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = rect.sizeDelta;
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 17f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }

        #endregion
    }
}
