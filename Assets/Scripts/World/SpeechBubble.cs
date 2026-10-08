using TMPro;
using UnityEngine;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// A parchment speech bubble that pops up above a character's head, always turned to the camera, and fades
    /// after a few seconds. Built once at runtime on first use and then only shown and hidden (no per-use
    /// allocations). Layer 2 (Ignore Raycast), so it never takes a click.
    /// </summary>
    public class SpeechBubble : MonoBehaviour
    {
        private static readonly Color PaperColor = new Color(0.95f, 0.89f, 0.74f, 1f);
        private static readonly Color TrimColor = new Color(0.36f, 0.24f, 0.13f, 1f);
        private static readonly Color InkColor = new Color(0.22f, 0.13f, 0.06f, 1f);

        private const float PopTime = 0.18f;
        private const float FadeTime = 0.3f;
        private const float BobHeight = 0.05f;
        private const float MaxTextWidth = 4.2f;

        private static Material s_paperMaterial;
        private static Material s_trimMaterial;
        private static Camera s_camera;

        private Transform board;
        private TextMeshPro label;
        private float shownAt = -100f;
        private float holdTime;

        /// <summary>True while the bubble is on screen.</summary>
        public bool IsShowing => board != null && board.gameObject.activeSelf;

        /// <summary>The text currently in the bubble.</summary>
        public string Text => label != null ? label.text : string.Empty;

        /// <summary>Shows <paramref name="text"/> for <paramref name="seconds"/>; showing again restarts it.</summary>
        public void Show(string text, float seconds)
        {
            if (board == null) Build();
            if (label.text != text) Resize(text);
            holdTime = seconds;
            shownAt = Time.time;
            board.gameObject.SetActive(true);
            Face();
        }

        private void LateUpdate()
        {
            if (board == null || !board.gameObject.activeSelf) return;
            float t = Time.time - shownAt;
            if (t > holdTime + FadeTime)
            {
                board.gameObject.SetActive(false);
                return;
            }
            float scale = t < PopTime ? Mathf.SmoothStep(0f, 1f, t / PopTime) * 1.06f
                        : t < PopTime * 1.6f ? Mathf.Lerp(1.06f, 1f, (t - PopTime) / (PopTime * 0.6f))
                        : t > holdTime ? Mathf.SmoothStep(1f, 0f, (t - holdTime) / FadeTime) : 1f;
            board.localScale = Vector3.one * Mathf.Max(0.001f, scale);
            board.localPosition = new Vector3(0f, Mathf.Sin(t * 2.2f) * BobHeight, 0f);
            Face();
        }

        private void Face()
        {
            if (s_camera == null || !s_camera.isActiveAndEnabled) s_camera = Camera.main;
            if (s_camera != null) board.rotation = s_camera.transform.rotation;
        }

        private void Build()
        {
            EnsureMaterials();
            board = new GameObject("Bubble").transform;
            board.gameObject.layer = 2;
            board.SetParent(transform, false);

            GameObject textGo = new GameObject("Bubble_Text");
            textGo.layer = 2;
            textGo.transform.SetParent(board, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.07f);
            label = textGo.AddComponent<TextMeshPro>();
            label.fontSize = 5.5f;
            label.fontStyle = FontStyles.Italic | FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = InkColor;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.text = string.Empty;
            board.gameObject.SetActive(false);
        }

        private void Resize(string text)
        {
            label.text = text;
            Vector2 size = label.GetPreferredValues(text, MaxTextWidth, 10f);
            float width = size.x + 0.7f;
            float height = size.y + 0.45f;
            label.rectTransform.sizeDelta = new Vector2(width, height);

            // Rebuild the panel to fit the line (only when the text changes)
            for (int i = board.childCount - 1; i >= 0; i--)
            {
                Transform c = board.GetChild(i);
                if (c != label.transform) Destroy(c.gameObject);
            }
            Cube("Trim", new Vector3(0f, 0f, 0.02f), new Vector3(width + 0.14f, height + 0.14f, 0.06f), s_trimMaterial);
            Cube("Paper", Vector3.zero, new Vector3(width, height, 0.1f), s_paperMaterial);
            // the tail points down at the speaker
            Transform tail = Cube("Tail_Trim", new Vector3(-width * 0.18f, -height * 0.5f - 0.12f, 0.02f), new Vector3(0.36f, 0.36f, 0.06f), s_trimMaterial);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tail = Cube("Tail", new Vector3(-width * 0.18f, -height * 0.5f - 0.07f, 0f), new Vector3(0.26f, 0.26f, 0.1f), s_paperMaterial);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private Transform Cube(string partName, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            go.layer = 2;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(board, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        private static void EnsureMaterials()
        {
            if (s_paperMaterial != null) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            s_paperMaterial = MakeLit(lit, "SpeechBubble_Paper", PaperColor);
            s_trimMaterial = MakeLit(lit, "SpeechBubble_Trim", TrimColor);
        }

        private static Material MakeLit(Shader shader, string matName, Color color)
        {
            Material m = new Material(shader) { name = matName };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            m.color = color;
            return m;
        }
    }
}
