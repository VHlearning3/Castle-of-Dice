using System.Collections;
using UnityEngine;
using TMPro;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Spawns floating combat numbers and milestone text banners (e.g. 'LEVEL UP!', '+5 HP', 'MISS')
    /// in 3D world space that float upward, scale gently, and fade out.
    /// </summary>
    public class FloatingCombatText : MonoBehaviour
    {
        #region Singleton

        public static FloatingCombatText Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Animation Configuration")]
        [SerializeField] private float floatSpeed = 1.6f;
        [SerializeField] private float lifetime = 1.4f;

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
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Displays floating text at world coordinates with custom color.
        /// </summary>
        public void ShowText(Vector3 worldPosition, string text, Color color)
        {
            StartCoroutine(AnimateText(worldPosition, text, color));
        }

        private IEnumerator AnimateText(Vector3 worldPosition, string text, Color color)
        {
            GameObject textObj = new GameObject("FloatingTextInstance");
            textObj.transform.position = worldPosition + Vector3.up * 1.5f;

            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 6.5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.outlineColor = Color.black;
            tmp.outlineWidth = 0.25f;

            Camera mainCam = Camera.main;
            float elapsed = 0f;
            Vector3 startPos = textObj.transform.position;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / lifetime;

                if (mainCam == null) mainCam = Camera.main;
                if (mainCam != null)
                {
                    // Face camera billboard style
                    textObj.transform.rotation = Quaternion.LookRotation(textObj.transform.position - mainCam.transform.position);
                }

                textObj.transform.position = startPos + Vector3.up * (progress * floatSpeed);

                // Fade out towards the end
                if (progress > 0.5f)
                {
                    float fade = 1f - ((progress - 0.5f) / 0.5f);
                    Color c = color;
                    c.a = fade;
                    tmp.color = c;
                }

                yield return null;
            }

            Destroy(textObj);
        }

        #endregion
    }
}
