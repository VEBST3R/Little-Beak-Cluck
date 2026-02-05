using Lean.Pool;
using UnityEngine;
using UnityEngine.UI;
using LittleBeakCluck.Combat;

namespace LittleBeakCluck.UI.DamageNumbers
{
    [DefaultExecutionOrder(-200)]
    public class DamageNumbersManager : MonoBehaviour
    {
        [SerializeField] private Canvas uiCanvas; // Screen Space - Overlay
        [SerializeField] private FloatingDamageText damageTextPrefab;
        [Header("Colors")]
        [SerializeField] private Color defaultColor = Color.yellow;
        [SerializeField] private Color highWaveColor = new Color(0.95f, 0.35f, 1.0f); // фіолетовий
        [SerializeField] private Color midWaveColor = new Color(1.0f, 0.86f, 0.25f);   // жовтий
        [SerializeField] private Color lowWaveColor = new Color(0.25f, 0.9f, 1.0f);    // бірюзовий
        [SerializeField] private Color playerDamageColor = new Color(1.0f, 0.35f, 0.35f); // червонуватий для урону по гравцю

        [Header("Position Jitter")]
        [Tooltip("Дрібний випадковий зсув позиції цифр, щоб вони не накладались при множинних хітах.")]
        [SerializeField] private bool useJitter = true;
        [Tooltip("Максимальний зсув по осях (за замовчуванням у пікселях відносно 1080p, масштабується до поточного Canvas).")]
        [SerializeField] private Vector2 jitterXY = new Vector2(14f, 8f);

        private static DamageNumbersManager _instance;
        private static bool _isQuitting = false;

        public static DamageNumbersManager Instance
        {
            get
            {
                if (_isQuitting)
                {
                    return null;
                }

                if (_instance == null)
                {
                    var go = new GameObject("DamageNumbersManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<DamageNumbersManager>();
                    _instance.EnsureCanvas();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            EnsureCanvas();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void EnsureCanvas()
        {
            if (uiCanvas == null)
            {
                var go = new GameObject("DamageNumbersCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                uiCanvas = go.GetComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                DontDestroyOnLoad(go);
            }
        }

        public void SpawnNumber(float amount, Vector3 worldPosition, Color? colorOverride = null)
        {
            if (damageTextPrefab == null || uiCanvas == null) return;
            Camera cam = Camera.main != null ? Camera.main : uiCanvas.worldCamera;
            if (cam == null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay) return;

            // 1) Отримуємо екранні координати в пікселях
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldPosition);

            // 2) Конвертуємо у локальні координати Canvas (центр Canvas = (0,0))
            var canvasRect = (RectTransform)uiCanvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
                out Vector2 localPoint);

            // 3) Застосувати невеликий випадковий зсув
            localPoint = ApplyJitter(localPoint, canvasRect);

            var instance = LeanPool.Spawn(damageTextPrefab, uiCanvas.transform);
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            instance.Show(Mathf.RoundToInt(amount).ToString(), localPoint, colorOverride ?? defaultColor);
        }

        // Spawns number as a child of a given RectTransform anchor (e.g., enemy's world-space UI root)
        public void SpawnNumberAtAnchor(float amount, Vector3 worldPosition, RectTransform anchor, Color? colorOverride = null)
        {
            if (damageTextPrefab == null || anchor == null)
                return;

            // If the anchor isn't under an active Canvas (or canvas not rendering), fallback to overlay spawn
            var parentCanvas = anchor.GetComponentInParent<Canvas>(includeInactive: true);
            if (parentCanvas == null || !parentCanvas.isActiveAndEnabled)
            {
                SpawnNumber(amount, worldPosition, colorOverride);
                return;
            }

            // Use the root canvas rect to avoid clipping by intermediate masks/RectMask2D on the bar hierarchy
            var targetRect = parentCanvas.transform as RectTransform;
            if (targetRect == null)
            {
                SpawnNumber(amount, worldPosition, colorOverride);
                return;
            }

            // Convert world point to the root canvas local space
            Vector3 local3 = targetRect.InverseTransformPoint(worldPosition);
            Vector2 localPoint = new Vector2(local3.x, local3.y);

            // Apply jitter in that canvas space
            localPoint = ApplyJitter(localPoint, targetRect);

            var instance = LeanPool.Spawn(damageTextPrefab, targetRect);
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            instance.Show(Mathf.RoundToInt(amount).ToString(), localPoint, colorOverride ?? defaultColor);
        }

        // Directly spawns under a RectTransform in its local space (no world conversion)
        public void SpawnNumberAtRectLocal(float amount, RectTransform anchor, Vector2 localPoint, Color? colorOverride = null)
        {
            if (damageTextPrefab == null || anchor == null)
                return;

            localPoint = ApplyJitter(localPoint, anchor);

            var instance = LeanPool.Spawn(damageTextPrefab, anchor);
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            instance.Show(Mathf.RoundToInt(amount).ToString(), localPoint, colorOverride ?? defaultColor);
        }

        public Color GetColorFor(VoiceWaveType type)
        {
            switch (type)
            {
                case VoiceWaveType.High: return highWaveColor;
                case VoiceWaveType.Mid: return midWaveColor;
                case VoiceWaveType.Low: return lowWaveColor;
                default: return defaultColor;
            }
        }

        public Color GetPlayerDamageColor() => playerDamageColor;

        private Vector2 ApplyJitter(Vector2 localPoint, RectTransform canvasRect)
        {
            if (!useJitter)
                return localPoint;

            if (canvasRect == null)
                return localPoint;

            float scale = canvasRect.rect.height > 0f ? (canvasRect.rect.height / 1080f) : 1f;
            Vector2 jitter = new Vector2(
                UnityEngine.Random.Range(-jitterXY.x, jitterXY.x),
                UnityEngine.Random.Range(-jitterXY.y, jitterXY.y)
            ) * scale;
            return localPoint + jitter;
        }
    }
}
