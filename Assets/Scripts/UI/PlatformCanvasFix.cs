using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LittleBeakCluck.UI
{
    /// <summary>
    /// Фіксить проблему з занадто великим UI на ПК/WebGL.
    /// На мобільних Canvas Scaler працює нормально, але на десктопі потрібні інші налаштування.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class PlatformCanvasFix : MonoBehaviour
    {
        [Header("Desktop/WebGL Settings")]
        [Tooltip("Reference resolution для ПК/WebGL (зазвичай більша ніж для мобільних)")]
        [SerializeField] private Vector2 desktopReferenceResolution = new Vector2(1920, 1080);
        [Tooltip("Match Width or Height для ПК/WebGL (0 = width, 1 = height, 0.5 = обидва)")]
        [SerializeField] private float desktopMatch = 0.5f;
        
        [Header("Mobile Settings")]
        [Tooltip("Reference resolution для мобільних")]
        [SerializeField] private Vector2 mobileReferenceResolution = new Vector2(1080, 1920);
        [Tooltip("Match Width or Height для мобільних")]
        [SerializeField] private float mobileMatch = 0.5f;
        
        [Header("World Space Canvas Settings")]
        [Tooltip("Якщо це World Space Canvas, масштаб для десктопу")]
        [SerializeField] private float desktopWorldCanvasScale = 0.8f;
        [Tooltip("Якщо це World Space Canvas, масштаб для мобільних")]
        [SerializeField] private float mobileWorldCanvasScale = 1f;

        private CanvasScaler _canvasScaler;
        private Canvas _canvas;

        private void Awake()
        {
            _canvasScaler = GetComponent<CanvasScaler>();
            _canvas = GetComponent<Canvas>();
            ApplyPlatformSettings();
        }

        private void ApplyPlatformSettings()
        {
            if (_canvasScaler == null) return;

            bool isMobile = IsMobilePlatform();

            // Якщо це World Space Canvas (ймовірно для Enemy HUD)
            if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace)
            {
                // Для World Space масштабуємо сам Canvas
                float scale = isMobile ? mobileWorldCanvasScale : desktopWorldCanvasScale;
                transform.localScale = Vector3.one * scale;
                
                // Встановлюємо Dynamic Pixels Per Unit для кращої якості
                _canvasScaler.dynamicPixelsPerUnit = 100f;
                
                Debug.Log($"[PlatformCanvasFix] World Space Canvas масштаб встановлено: {scale} (Mobile: {isMobile})");
                return;
            }

            // Для Screen Space Canvas
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            
            if (isMobile)
            {
                _canvasScaler.referenceResolution = mobileReferenceResolution;
                _canvasScaler.matchWidthOrHeight = mobileMatch;
            }
            else
            {
                _canvasScaler.referenceResolution = desktopReferenceResolution;
                _canvasScaler.matchWidthOrHeight = desktopMatch;
            }

            // Встановлюємо Physical Unit для кращої консистентності
            _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            
            Debug.Log($"[PlatformCanvasFix] Canvas Scaler налаштовано для {(isMobile ? "Mobile" : "Desktop/WebGL")}: " +
                     $"Resolution={_canvasScaler.referenceResolution}, Match={_canvasScaler.matchWidthOrHeight}");
        }

        private bool IsMobilePlatform()
        {
#if UNITY_EDITOR
            // В Editor перевіряємо Build Target
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            return buildTarget == BuildTarget.Android || buildTarget == BuildTarget.iOS;
#else
            return Application.isMobilePlatform;
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Для тестування в редакторі
            if (Application.isPlaying && _canvasScaler != null)
            {
                ApplyPlatformSettings();
            }
        }
#endif
    }
}
