using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Spawns floating combat numbers and milestone text banners (e.g. 'LEVEL UP!', '+5 HP', 'MISS')
    /// in 3D world space that float upward, scale gently, and fade out.
    /// Text objects are pooled (WebGL zero-GC rule): no Instantiate/Destroy per damage number.
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

        [Header("Pooling")]
        [Tooltip("Text objects created up front; the pool grows only if more popups are visible at once.")]
        [SerializeField] private int initialPoolSize = 12;

        #endregion

        #region Private State

        private readonly Stack<TextMeshPro> pool = new Stack<TextMeshPro>();

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

            for (int i = 0; i < initialPoolSize; i++)
            {
                pool.Push(CreatePooledText());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
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

        #endregion

        #region Pooling & Animation

        private TextMeshPro CreatePooledText()
        {
            GameObject textObj = new GameObject("FloatingTextInstance");
            textObj.transform.SetParent(transform, false);

            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            tmp.fontSize = 6.5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.outlineColor = Color.black;
            tmp.outlineWidth = 0.25f;

            textObj.SetActive(false);
            return tmp;
        }

        private IEnumerator AnimateText(Vector3 worldPosition, string text, Color color)
        {
            TextMeshPro tmp = pool.Count > 0 ? pool.Pop() : CreatePooledText();
            Transform textTransform = tmp.transform;

            tmp.text = text;
            tmp.color = color;
            textTransform.position = worldPosition + Vector3.up * 1.5f;
            tmp.gameObject.SetActive(true);

            Camera mainCam = Camera.main;
            float elapsed = 0f;
            Vector3 startPos = textTransform.position;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / lifetime;

                if (mainCam == null) mainCam = Camera.main;
                if (mainCam != null)
                {
                    // Face camera billboard style
                    textTransform.rotation = Quaternion.LookRotation(textTransform.position - mainCam.transform.position);
                }

                textTransform.position = startPos + Vector3.up * (progress * floatSpeed);

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

            tmp.gameObject.SetActive(false);
            pool.Push(tmp);
        }

        #endregion
    }
}
