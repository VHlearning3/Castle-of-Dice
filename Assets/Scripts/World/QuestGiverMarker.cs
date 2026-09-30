using UnityEngine;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// What a quest giver's head marker shows.
    /// </summary>
    public enum QuestMarkerKind
    {
        /// <summary>No marker (quest done).</summary>
        None,

        /// <summary>Yellow "!": the villager has work to offer.</summary>
        Available,

        /// <summary>Grey "?": quest accepted, objective not done yet.</summary>
        InProgress,

        /// <summary>Yellow "?": objective done, come back for the reward.</summary>
        ReadyToTurnIn
    }

    /// <summary>
    /// Floating "!" / "?" above a quest giver's head, kept up to date from QuestManager events.
    /// Added at runtime by VillageNPC for every villager that gives a quest.
    /// </summary>
    public class QuestGiverMarker : MonoBehaviour
    {
        #region Constants

        private const float HeadClearance = 0.55f;
        private const float BobHeight = 0.08f;
        private const float BobSpeed = 2.4f;

        private static readonly Color AvailableColor = new Color(1f, 0.84f, 0.12f, 1f);
        private static readonly Color ReadyColor = new Color(1f, 0.84f, 0.12f, 1f);
        private static readonly Color InProgressColor = new Color(0.72f, 0.74f, 0.78f, 1f);

        #endregion

        #region Private State

        private string questId;
        private Transform markerRoot;
        private TextMeshPro markerText;
        private Vector3 baseLocalPosition;
        private Camera cachedCamera;

        #endregion

        #region Public Properties

        /// <summary>Quest this marker follows.</summary>
        public string QuestId => questId;

        /// <summary>What the marker currently shows.</summary>
        public QuestMarkerKind CurrentKind { get; private set; } = QuestMarkerKind.None;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            QuestManager.OnQuestStateUpdated += HandleQuestStateUpdated;
            QuestManager.OnQuestProgressUpdated += HandleQuestProgressUpdated;
            Refresh();
        }

        private void OnDisable()
        {
            QuestManager.OnQuestStateUpdated -= HandleQuestStateUpdated;
            QuestManager.OnQuestProgressUpdated -= HandleQuestProgressUpdated;
        }

        private void LateUpdate()
        {
            if (markerRoot == null || !markerRoot.gameObject.activeSelf) return;

            if (cachedCamera == null) cachedCamera = Camera.main;
            if (cachedCamera != null)
            {
                markerRoot.rotation = Quaternion.LookRotation(markerRoot.position - cachedCamera.transform.position);
            }

            markerRoot.localPosition = baseLocalPosition + Vector3.up * (Mathf.Sin(Time.time * BobSpeed) * BobHeight / SafeScale(transform.lossyScale.y));
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts following <paramref name="id"/> and builds the floating marker.
        /// </summary>
        public void Configure(string id)
        {
            questId = id;
            EnsureMarker();
            Refresh();
        }

        /// <summary>
        /// Re-reads the quest state and updates the symbol.
        /// </summary>
        public void Refresh()
        {
            if (string.IsNullOrEmpty(questId)) return;

            QuestManager quests = QuestManager.Instance;
            if (quests == null) return;

            CurrentKind = ResolveMarker(quests.GetQuestState(questId), quests.IsReadyToTurnIn(questId));
            ApplyKind();
        }

        /// <summary>
        /// Picks the marker for a quest state.
        /// </summary>
        public static QuestMarkerKind ResolveMarker(QuestState state, bool readyToTurnIn)
        {
            switch (state)
            {
                case QuestState.NotStarted: return QuestMarkerKind.Available;
                case QuestState.InProgress: return readyToTurnIn ? QuestMarkerKind.ReadyToTurnIn : QuestMarkerKind.InProgress;
                default: return QuestMarkerKind.None;
            }
        }

        /// <summary>
        /// The character shown for a marker ("!", "?" or empty).
        /// </summary>
        public static string SymbolFor(QuestMarkerKind kind)
        {
            switch (kind)
            {
                case QuestMarkerKind.Available: return "!";
                case QuestMarkerKind.InProgress:
                case QuestMarkerKind.ReadyToTurnIn: return "?";
                default: return "";
            }
        }

        #endregion

        #region Internals

        private void HandleQuestStateUpdated(string id, QuestState state)
        {
            if (string.Equals(id, questId, System.StringComparison.OrdinalIgnoreCase)) Refresh();
        }

        private void HandleQuestProgressUpdated(string id, int current, int required)
        {
            if (string.Equals(id, questId, System.StringComparison.OrdinalIgnoreCase)) Refresh();
        }

        private void EnsureMarker()
        {
            if (markerRoot != null) return;

            // Created with a RectTransform up front so TextMeshPro doesn't swap the transform out later
            GameObject markerObj = new GameObject("Quest_Marker", typeof(RectTransform));
            markerRoot = markerObj.transform;
            markerRoot.SetParent(transform, false);

            // Undo the NPC's scale so every marker is the same size
            Vector3 lossy = transform.lossyScale;
            markerRoot.localScale = new Vector3(1f / SafeScale(lossy.x), 1f / SafeScale(lossy.y), 1f / SafeScale(lossy.z));

            float headY = ResolveHeadHeight();
            baseLocalPosition = transform.InverseTransformPoint(new Vector3(transform.position.x, headY + HeadClearance, transform.position.z));
            markerRoot.localPosition = baseLocalPosition;

            markerText = markerObj.AddComponent<TextMeshPro>();
            markerText.text = "";
            markerText.fontSize = 9f;
            markerText.fontStyle = FontStyles.Bold;
            markerText.alignment = TextAlignmentOptions.Center;
            markerText.rectTransform.sizeDelta = new Vector2(1.5f, 1.5f);
            markerText.outlineWidth = 0.25f;
            markerText.outlineColor = new Color32(40, 24, 8, 255);
        }

        private float ResolveHeadHeight()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            bool found = false;
            float top = transform.position.y + 2f;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                float y = r.bounds.max.y;
                if (!found || y > top)
                {
                    top = y;
                    found = true;
                }
            }
            return found ? top : transform.position.y + 2f;
        }

        private void ApplyKind()
        {
            if (markerRoot == null) return;

            bool visible = CurrentKind != QuestMarkerKind.None;
            markerRoot.gameObject.SetActive(visible);
            if (!visible || markerText == null) return;

            markerText.text = SymbolFor(CurrentKind);
            markerText.color = CurrentKind == QuestMarkerKind.InProgress
                ? InProgressColor
                : (CurrentKind == QuestMarkerKind.ReadyToTurnIn ? ReadyColor : AvailableColor);
        }

        private static float SafeScale(float value)
        {
            return Mathf.Abs(value) < 0.0001f ? 1f : value;
        }

        #endregion
    }
}
