using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// A letter or diary page lying in a zone (critical review C3). Clicking it opens the text on the
    /// story panel; the page glows until it has been read once (remembered in the save as a story flag).
    /// </summary>
    public class LoreNote : Interactable
    {
        [Tooltip("Id of the text in LoreTexts.")]
        [SerializeField] private string noteId = "";

        [Tooltip("Small light that marks an unread note.")]
        [SerializeField] private Light unreadGlow;

        /// <summary>Id of the note's text.</summary>
        public string NoteId
        {
            get => noteId;
            set => noteId = value;
        }

        protected override void Awake()
        {
            base.Awake();
            LoreTexts.Note note = LoreTexts.Get(noteId);
            promptMessage = "Read: " + note.Title;
            interactionRadius = Mathf.Max(interactionRadius, 3f);
        }

        private void Start()
        {
            RefreshGlow();
        }

        public override void Interact(PlayerUnit player)
        {
            LoreTexts.Note note = LoreTexts.Get(noteId);
            StoryFlags.Set(LoreTexts.ReadFlag(noteId));
            RefreshGlow();
            StoryPanelUI.Show(note.Title, note.Body);
        }

        private void RefreshGlow()
        {
            if (unreadGlow != null) unreadGlow.enabled = !StoryFlags.Has(LoreTexts.ReadFlag(noteId));
        }

        /// <summary>
        /// Builds a note at runtime: a small parchment block with a soft light (used for Oakhaven, whose
        /// scene stays untouched, and by the zone content builder).
        /// </summary>
        public static LoreNote Create(string id, Vector3 position, float yaw, Transform parent = null)
        {
            GameObject root = new GameObject("LoreNote_" + id);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paper.name = "Parchment";
            paper.transform.SetParent(root.transform, false);
            paper.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            paper.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
            paper.transform.localScale = new Vector3(0.45f, 0.04f, 0.6f);
            Renderer r = paper.GetComponent<Renderer>();
            if (r != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    Material mat = new Material(shader) { name = "Parchment_Mat" };
                    Color parchment = new Color(0.86f, 0.78f, 0.6f);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", parchment);
                    mat.color = parchment;
                    r.sharedMaterial = mat;
                }
            }

            BoxCollider click = root.AddComponent<BoxCollider>();
            click.center = new Vector3(0f, 0.4f, 0f);
            click.size = new Vector3(1.2f, 0.8f, 1.2f);
            click.isTrigger = false;

            GameObject glowObj = new GameObject("Unread_Glow");
            glowObj.transform.SetParent(root.transform, false);
            glowObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            Light glow = glowObj.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.85f, 0.5f);
            glow.range = 2.5f;
            glow.intensity = 1.5f;

            LoreNote note = root.AddComponent<LoreNote>();
            note.noteId = id;
            note.unreadGlow = glow;
            LoreTexts.Note text = LoreTexts.Get(id);
            note.promptMessage = "Read: " + text.Title;
            return note;
        }
    }
}
