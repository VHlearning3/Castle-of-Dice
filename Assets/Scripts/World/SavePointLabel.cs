using TMPro;
using UnityEngine;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Floating name over a save shrine ("Ancient Runestone Shrine", with a small "Rest &amp; Save" line), built in
    /// code so the hand-edited village scene is never touched. After a save the caption turns into a green
    /// "Game Saved" that pops and fades back. Faces the camera and sits on layer 2 so it never takes clicks.
    /// </summary>
    public class SavePointLabel : MonoBehaviour
    {
        public const string SavedText = "Game Saved";
        public const string CaptionText = "Rest & Save";
        public const float SavedSeconds = 3f;

        private const float BobHeight = 0.06f;
        private const float PullToCamera = 1.4f;
        private static readonly Color SavedColor = new Color(0.45f, 1f, 0.6f, 1f);
        private static readonly Color CaptionColor = new Color(0.62f, 0.86f, 1f, 1f);

        private static Camera s_camera;
        private static Sprite s_plateSprite;

        private Transform board;
        private Vector3 boardBase;
        private TextMeshPro saved;
        private float savedUntil = -1f;
        private float seed;

        /// <summary>The shrine name on the label.</summary>
        public TextMeshPro Title { get; private set; }

        /// <summary>The small line under the name; reads "Game Saved" for a moment after a save.</summary>
        public TextMeshPro Caption { get; private set; }

        /// <summary>Whether the "Game Saved" text is showing.</summary>
        public bool IsShowingSaved => savedUntil > Time.time;

        /// <summary>Builds the label over <paramref name="shrine"/> (once).</summary>
        public static SavePointLabel Attach(SavePoint shrine)
        {
            if (shrine == null) return null;
            SavePointLabel existing = shrine.GetComponentInChildren<SavePointLabel>(true);
            if (existing != null) return existing;

            GameObject go = new GameObject("SavePoint_Label");
            go.layer = 2; // Ignore Raycast
            go.transform.SetParent(shrine.transform, false);
            go.transform.position = new Vector3(shrine.transform.position.x, TopOf(shrine.transform) + 1.1f, shrine.transform.position.z);
            SavePointLabel label = go.AddComponent<SavePointLabel>();
            label.Build(shrine.ShrineName);
            return label;
        }

        /// <summary>Shows "Game Saved" for a few seconds.</summary>
        public void ShowSaved()
        {
            savedUntil = Time.time + SavedSeconds;
            if (Caption != null) Caption.gameObject.SetActive(false);
            if (saved != null) saved.gameObject.SetActive(true);
        }

        private void Build(string shrineName)
        {
            seed = Random.value * 10f;
            board = new GameObject("Board").transform;
            board.gameObject.layer = 2;
            board.SetParent(transform, false);
            boardBase = Vector3.zero;

            Title = MakeText("Name_Text", string.IsNullOrEmpty(shrineName) ? "Runestone Shrine" : shrineName,
                3.4f, FontStyles.Bold | FontStyles.SmallCaps, UITheme.GoldAccent, new Vector3(0f, 0.22f, 0f));
            Caption = MakeText("Caption_Text", CaptionText, 2.3f, FontStyles.Bold, CaptionColor, new Vector3(0f, -0.22f, 0f));
            Caption.characterSpacing = 4f;
            saved = MakeText("Saved_Text", SavedText, 3f, FontStyles.Bold, SavedColor, new Vector3(0f, -0.26f, 0f));
            saved.gameObject.SetActive(false);

            if (Application.isPlaying) BuildPlate();
        }

        // Dark see-through plate behind the text, so it reads over the shrine's glow and the bright grass
        private void BuildPlate()
        {
            if (s_plateSprite == null)
            {
                Texture2D white = Texture2D.whiteTexture;
                s_plateSprite = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
            }

            GameObject go = new GameObject("Plate");
            go.layer = 2;
            go.transform.SetParent(board, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            float width = Title.GetPreferredValues(Title.text, 20f, 1f).x + 0.6f;
            go.transform.localScale = new Vector3(width, 1.0f, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s_plateSprite;
            sr.color = new Color(0.03f, 0.03f, 0.06f, 0.55f);
            sr.sortingOrder = 49;
            sr.material.renderQueue = 3999;
        }

        private TextMeshPro MakeText(string name, string text, float size, FontStyles style, Color color, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(board, false);
            go.transform.localPosition = localPos;
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            // The outline makes its own material instance, which leaks outside Play Mode
            if (Application.isPlaying)
            {
                tmp.outlineColor = new Color32(12, 8, 4, 255);
                tmp.outlineWidth = 0.25f;
                tmp.fontMaterial.renderQueue = 4000; // after the Great Hall shrine's see-through light beam
            }
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.sortingOrder = 50; // after the Great Hall shrine's see-through light beam
            tmp.rectTransform.sizeDelta = new Vector2(8f, 0.6f);
            return tmp;
        }

        private void LateUpdate()
        {
            if (board == null) return;
            float t = Time.time;
            board.localPosition = boardBase + new Vector3(0f, Mathf.Sin(t * 1.4f + seed) * BobHeight, 0f);

            if (saved != null && saved.gameObject.activeSelf)
            {
                float left = savedUntil - t;
                if (left <= 0f)
                {
                    saved.gameObject.SetActive(false);
                    if (Caption != null) Caption.gameObject.SetActive(true);
                }
                else
                {
                    // Quick pop in, then fade out over the last second
                    float age = SavedSeconds - left;
                    saved.transform.localScale = Vector3.one * (1f + 0.35f * Mathf.Clamp01(1f - age * 4f));
                    Color c = SavedColor;
                    c.a = Mathf.Clamp01(left);
                    saved.color = c;
                }
            }

            if (s_camera == null || !s_camera.isActiveAndEnabled) s_camera = Camera.main;
            if (s_camera != null)
            {
                board.rotation = s_camera.transform.rotation;
                // Pulled toward the camera so the shrine's own bowl, crystal or light pillar never covers it
                board.position -= s_camera.transform.forward * PullToCamera;
            }
        }

        private static float TopOf(Transform root)
        {
            bool any = false;
            Bounds b = default;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r is ParticleSystemRenderer || r.GetComponent<TMP_Text>() != null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? Mathf.Min(b.max.y, root.position.y + 3f) : root.position.y + 1.5f;
        }
    }
}
