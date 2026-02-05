using UnityEngine;

namespace LittleBeakCluck.UI.Settings
{
    [DisallowMultipleComponent]
    public class ControlLayoutSwapper : MonoBehaviour
    {
        [Header("Assign the rects to swap")]
        [Tooltip("Container holding the joystick(s) UI. Usually bottom-left.")]
        [SerializeField] private RectTransform joystickGroup;

        [Tooltip("Container holding the attack button UI. Usually bottom-right.")]
        [SerializeField] private RectTransform attackButtonGroup;

        [Header("Optional animation")]
        [SerializeField] private float tweenDuration = 0f; // 0 = instant

        [Header("Manual X overrides (optional)")]
        [Tooltip("If enabled, overrides the anchoredPosition.x for each group in NORMAL (not inverted) state.")]
        [SerializeField] private bool useManualX = false;
        [SerializeField] private float normalJoystickX = 0f;
        [SerializeField] private float normalAttackX = 0f;
        [Tooltip("If enabled, overrides the anchoredPosition.x for each group in INVERTED state. Uses the same 'Use Manual X' flag.")]
        [SerializeField] private float invertedJoystickX = 0f;
        [SerializeField] private float invertedAttackX = 0f;

        private Vector2 _jAnchoredPos;
        private Vector2 _aAnchoredPos;
        private Vector2 _jAnchorMin, _jAnchorMax, _jPivot;
        private Vector2 _aAnchorMin, _aAnchorMax, _aPivot;
        private bool _appliedOnce;

        private void OnEnable()
        {
            // Cache after potential Start() changes from other scripts
            CacheOriginals();
            ControlLayoutSettings.InvertedChanged += OnInvertedChanged;
            Apply(ControlLayoutSettings.IsInverted, true);
        }

        private void OnDisable()
        {
            ControlLayoutSettings.InvertedChanged -= OnInvertedChanged;
        }

        private void CacheOriginals()
        {
            if (joystickGroup != null)
            {
                _jAnchoredPos = joystickGroup.anchoredPosition;
                _jAnchorMin = joystickGroup.anchorMin;
                _jAnchorMax = joystickGroup.anchorMax;
                _jPivot = joystickGroup.pivot;
            }

            if (attackButtonGroup != null)
            {
                _aAnchoredPos = attackButtonGroup.anchoredPosition;
                _aAnchorMin = attackButtonGroup.anchorMin;
                _aAnchorMax = attackButtonGroup.anchorMax;
                _aPivot = attackButtonGroup.pivot;
            }
        }

        private void OnInvertedChanged(bool inverted)
        {
            Apply(inverted, false);
        }

        private void Apply(bool inverted, bool firstApply)
        {
            if (joystickGroup == null || attackButtonGroup == null)
                return;

            // Determine target anchors/pivots and base positions for both groups.
            // Preserve Y: keep each element's original Y for anchorMin.y, anchorMax.y, pivot.y and anchoredPosition.y.
            Vector2 jAnchorMin = _jAnchorMin;
            Vector2 jAnchorMax = _jAnchorMax;
            Vector2 jPivot = _jPivot;
            Vector2 jPos = _jAnchoredPos; // we will only change jPos.x

            Vector2 aAnchorMin = _aAnchorMin;
            Vector2 aAnchorMax = _aAnchorMax;
            Vector2 aPivot = _aPivot;
            Vector2 aPos = _aAnchoredPos; // only change aPos.x

            if (!inverted)
            {
                // Normal: keep each group's X-side anchors/pivot as originally set
                // (already initialized). Only override X position if requested.
                if (useManualX)
                {
                    jPos.x = normalJoystickX;
                    aPos.x = normalAttackX;
                }
            }
            else
            {
                // Inverted: swap only the X-components of anchors/pivot between groups.
                // Preserve Y-components from their own originals.

                // Joystick gets Attack's X-anchors/pivot.x
                jAnchorMin.x = _aAnchorMin.x;
                jAnchorMax.x = _aAnchorMax.x;
                jPivot.x = _aPivot.x;
                // Attack gets Joystick's X-anchors/pivot.x
                aAnchorMin.x = _jAnchorMin.x;
                aAnchorMax.x = _jAnchorMax.x;
                aPivot.x = _jPivot.x;

                // Positions: start from each own original Y, adjust X per mode
                jPos.x = useManualX ? invertedJoystickX : _aAnchoredPos.x;
                aPos.x = useManualX ? invertedAttackX : _jAnchoredPos.x;
            }

            SetRectTransform(joystickGroup, jAnchorMin, jAnchorMax, jPivot, jPos, firstApply);
            SetRectTransform(attackButtonGroup, aAnchorMin, aAnchorMax, aPivot, aPos, firstApply);

            _appliedOnce = true;
        }

        private void SetRectTransform(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, bool instant)
        {
            if (rt == null)
                return;

            // We preserve sizeDelta (width/height) and parent.
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;

            if (tweenDuration <= 0f || instant)
            {
                rt.anchoredPosition = anchoredPos;
            }
            else
            {
                // Simple manual tween without external deps
                StopAllCoroutines();
                StartCoroutine(TweenAnchoredPosition(rt, anchoredPos, tweenDuration));
            }
        }

        private System.Collections.IEnumerator TweenAnchoredPosition(RectTransform rt, Vector2 target, float duration)
        {
            Vector2 start = rt.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                rt.anchoredPosition = Vector2.LerpUnclamped(start, target, EaseInOutQuad(u));
                yield return null;
            }
            rt.anchoredPosition = target;
        }

        private static float EaseInOutQuad(float x)
        {
            return x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f;
        }
    }
}
