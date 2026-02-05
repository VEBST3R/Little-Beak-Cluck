using System.Collections;
using Lean.Pool;
using UnityEngine;
using TMPro;

namespace LittleBeakCluck.UI.DamageNumbers
{
    public class FloatingDamageText : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private float lifetime = 0.8f;
        [SerializeField] private Vector2 startOffset = new Vector2(0f, 24f);
        [SerializeField] private Vector2 endOffset = new Vector2(0f, 72f);
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [SerializeField] private TMP_Text label;

        private RectTransform _rect;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        // Expecting a local point in the parent Canvas' rect (0,0 = canvas center)
        public void Show(string text, Vector2 canvasLocalPoint, Color color)
        {
            if (label == null || _rect == null) return;
            label.text = text;
            label.color = color;
            // Convert pixel-like offsets to local canvas units respecting current scale
            var canvas = GetComponentInParent<Canvas>();
            var rtCanvas = canvas != null ? (RectTransform)canvas.transform : null;
            Vector2 scaledStart = startOffset;
            if (rtCanvas != null && rtCanvas.rect.height > 0)
            {
                // Normalize to 1080p reference similar to CanvasScaler
                float scale = rtCanvas.rect.height / 1080f;
                scaledStart *= scale;
            }
            _rect.anchoredPosition = canvasLocalPoint + scaledStart;
            StopAllCoroutines();
            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            float t = 0f;
            var start = _rect.anchoredPosition;
            var canvas = GetComponentInParent<Canvas>();
            var rtCanvas = canvas != null ? (RectTransform)canvas.transform : null;
            Vector2 scaledEnd = endOffset;
            if (rtCanvas != null && rtCanvas.rect.height > 0)
            {
                float scale = rtCanvas.rect.height / 1080f;
                scaledEnd *= scale;
            }
            var end = new Vector2(start.x + scaledEnd.x, start.y + scaledEnd.y);
            var baseColor = label.color;
            while (t < lifetime)
            {
                float p = Mathf.Clamp01(t / lifetime);
                _rect.anchoredPosition = Vector2.LerpUnclamped(start, end, p);
                float a = alphaCurve.Evaluate(p);
                label.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                t += Time.deltaTime;
                yield return null;
            }
            LeanPool.Despawn(gameObject);
        }
    }
}
