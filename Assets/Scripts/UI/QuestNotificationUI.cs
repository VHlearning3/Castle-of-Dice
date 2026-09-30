using System.Collections.Generic;
using UnityEngine;
using TMPro;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Top-center banner that announces quest events with a sound: new quest, progress, objective done
    /// (return to the giver) and quest complete with its rewards. Messages queue and show one at a time.
    /// </summary>
    public class QuestNotificationUI : MonoBehaviour
    {
        #region Types

        /// <summary>One queued banner.</summary>
        public struct QuestNotice
        {
            public string Header;
            public string Body;
            public Color HeaderColor;
            public SFXClipType Sound;
            public float Volume;
        }

        #endregion

        #region Constants

        private const float FadeInSeconds = 0.25f;
        private const float HoldSeconds = 2.8f;
        private const float FadeOutSeconds = 0.45f;

        private static readonly Color NewQuestColor = new Color(0.965f, 0.835f, 0.47f, 1f);
        private static readonly Color ProgressColor = new Color(0.85f, 0.88f, 0.92f, 1f);
        private static readonly Color DoneColor = new Color(0.18f, 0.80f, 0.44f, 1f);

        #endregion

        #region Singleton

        public static QuestNotificationUI Instance { get; private set; }

        #endregion

        #region Private State

        private readonly Queue<QuestNotice> pending = new Queue<QuestNotice>();
        private CanvasGroup bannerGroup;
        private TMP_Text headerText;
        private TMP_Text bodyText;
        private bool isShowing;
        private float shownAt;

        #endregion

        #region Public Properties

        /// <summary>Banners waiting to be shown (not counting the one on screen).</summary>
        public int PendingCount => pending.Count;

        /// <summary>Header of the banner on screen (empty when none).</summary>
        public string CurrentHeader => isShowing && headerText != null ? headerText.text : "";

        /// <summary>Body of the banner on screen (empty when none).</summary>
        public string CurrentBody => isShowing && bodyText != null ? bodyText.text : "";

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            EnsureBanner();
        }

        private void OnEnable()
        {
            QuestManager.OnQuestAccepted += HandleQuestAccepted;
            QuestManager.OnQuestObjectiveAdvanced += HandleObjectiveAdvanced;
            QuestManager.OnQuestReadyToTurnIn += HandleReadyToTurnIn;
            QuestManager.OnQuestCompleted += HandleQuestCompleted;
        }

        private void OnDisable()
        {
            QuestManager.OnQuestAccepted -= HandleQuestAccepted;
            QuestManager.OnQuestObjectiveAdvanced -= HandleObjectiveAdvanced;
            QuestManager.OnQuestReadyToTurnIn -= HandleReadyToTurnIn;
            QuestManager.OnQuestCompleted -= HandleQuestCompleted;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!isShowing)
            {
                if (pending.Count > 0) ShowNext();
                return;
            }

            float elapsed = Time.unscaledTime - shownAt;
            if (elapsed < FadeInSeconds)
            {
                bannerGroup.alpha = elapsed / FadeInSeconds;
            }
            else if (elapsed < FadeInSeconds + HoldSeconds)
            {
                bannerGroup.alpha = 1f;
            }
            else if (elapsed < FadeInSeconds + HoldSeconds + FadeOutSeconds)
            {
                bannerGroup.alpha = 1f - (elapsed - FadeInSeconds - HoldSeconds) / FadeOutSeconds;
            }
            else
            {
                HideBanner();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Queues a banner; it shows (with its sound) once the ones before it have faded.
        /// </summary>
        public void Enqueue(QuestNotice notice)
        {
            pending.Enqueue(notice);
            if (!isShowing) ShowNext();
        }

        /// <summary>Banner for a newly accepted quest.</summary>
        public static QuestNotice BuildAccepted(QuestSO quest)
        {
            string body = $"<b>{quest.QuestTitle}</b>\n{quest.ObjectiveSummary}";
            if (!string.IsNullOrEmpty(quest.ObjectiveLocation)) body += $" <color=#A0AEC0>({quest.ObjectiveLocation})</color>";
            return new QuestNotice { Header = "NEW QUEST", Body = body, HeaderColor = NewQuestColor, Sound = SFXClipType.QuestComplete, Volume = 0.55f };
        }

        /// <summary>Banner for objective progress.</summary>
        public static QuestNotice BuildProgress(QuestSO quest, int current, int required)
        {
            string body = $"<b>{quest.QuestTitle}</b>\n{quest.ObjectiveSummary}: <color=#F1C40F>{current}/{required}</color>";
            return new QuestNotice { Header = "QUEST UPDATED", Body = body, HeaderColor = ProgressColor, Sound = SFXClipType.ButtonClick, Volume = 0.8f };
        }

        /// <summary>Banner for a finished objective.</summary>
        public static QuestNotice BuildReady(QuestSO quest)
        {
            string giver = string.IsNullOrEmpty(quest.QuestGiverName) ? "the quest giver" : quest.QuestGiverName;
            string body = $"<b>{quest.QuestTitle}</b>\nReturn to {giver} for your reward.";
            return new QuestNotice { Header = "OBJECTIVE COMPLETE", Body = body, HeaderColor = DoneColor, Sound = SFXClipType.QuestComplete, Volume = 0.8f };
        }

        /// <summary>Banner for a handed-in quest listing its rewards.</summary>
        public static QuestNotice BuildCompleted(QuestSO quest, int gold, ItemSO rewardItem, int itemAmount)
        {
            string body = $"<b>{quest.QuestTitle}</b>\n{FormatRewards(gold, rewardItem, itemAmount)}";
            return new QuestNotice { Header = "QUEST COMPLETE", Body = body, HeaderColor = DoneColor, Sound = SFXClipType.QuestComplete, Volume = 1f };
        }

        /// <summary>
        /// "+45 gold, 2x Health Potion" style reward text.
        /// </summary>
        public static string FormatRewards(int gold, ItemSO rewardItem, int itemAmount)
        {
            string text = gold > 0 ? $"<color=#F1C40F>+{gold} gold</color>" : "";
            if (rewardItem != null)
            {
                string item = itemAmount > 1 ? $"{itemAmount}x {rewardItem.ItemName}" : rewardItem.ItemName;
                text = string.IsNullOrEmpty(text) ? item : $"{text}, {item}";
            }
            return string.IsNullOrEmpty(text) ? "Thanks received." : text;
        }

        #endregion

        #region Event Handlers

        private void HandleQuestAccepted(QuestSO quest)
        {
            if (quest != null) Enqueue(BuildAccepted(quest));
        }

        private void HandleObjectiveAdvanced(QuestSO quest, int current, int required)
        {
            // Reaching the goal gets its own "return to" banner
            if (quest != null && current < required) Enqueue(BuildProgress(quest, current, required));
        }

        private void HandleReadyToTurnIn(QuestSO quest)
        {
            if (quest != null) Enqueue(BuildReady(quest));
        }

        private void HandleQuestCompleted(QuestSO quest, int gold)
        {
            if (quest == null) return;
            QuestManager quests = QuestManager.Instance;
            ItemSO item = quests != null ? quests.GetRewardItem(quest) : quest.RewardItem;
            Enqueue(BuildCompleted(quest, gold, item, quest.RewardItemAmount));
        }

        #endregion

        #region Display

        private void ShowNext()
        {
            if (pending.Count == 0) return;
            EnsureBanner();

            QuestNotice notice = pending.Dequeue();
            headerText.text = notice.Header;
            headerText.color = notice.HeaderColor;
            bodyText.text = notice.Body;

            bannerGroup.gameObject.SetActive(true);
            bannerGroup.alpha = 0f;
            isShowing = true;
            shownAt = Time.unscaledTime;

            SFXManager.Instance?.PlaySFX(notice.Sound, default, notice.Volume);
        }

        private void HideBanner()
        {
            isShowing = false;
            if (bannerGroup != null)
            {
                bannerGroup.alpha = 0f;
                bannerGroup.gameObject.SetActive(false);
            }
        }

        private void EnsureBanner()
        {
            if (bannerGroup != null) return;

            Canvas canvas = QuestUIBuilder.CreateOverlayCanvas(transform, "Quest_Notification_Canvas", 60);

            // Top center, below the zone banner
            RectTransform panel = QuestUIBuilder.CreatePanel(canvas.transform, "Quest_Notification_Banner",
                new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(560f, 86f), new Color(0.06f, 0.07f, 0.1f, 0.94f));
            bannerGroup = panel.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.interactable = false;

            headerText = QuestUIBuilder.CreateLabel(panel, "Header", 15f, FontStyles.Bold, NewQuestColor,
                TextAlignmentOptions.Top, new Vector4(18f, 10f, 18f, 50f));
            headerText.characterSpacing = 2f;

            bodyText = QuestUIBuilder.CreateLabel(panel, "Body", 15f, FontStyles.Normal, UITheme.CreamText,
                TextAlignmentOptions.Top, new Vector4(18f, 32f, 18f, 6f));

            panel.gameObject.SetActive(false);
        }

        #endregion
    }
}
