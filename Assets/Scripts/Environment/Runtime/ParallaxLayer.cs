using UnityEngine;

namespace LittleBeakCluck.Environment
{
    [ExecuteAlways]
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Parallax Settings")]
        [Tooltip("0 = рухається разом з камерою, 1 = статично (дальній план)")]
        [Range(0f, 1f)] public float horizontalParallax = 0.5f;
        [Range(0f, 1f)] public float verticalParallax = 0f;
        [SerializeField] private bool lockY = true;

        [Header("Smoothing / Pixel Perfect")]
        [SerializeField] private bool smooth = false; // здебільшого НЕ треба – беріть чисту позицію
        [SerializeField] private float smoothSpeed = 8f;
        [SerializeField] private bool pixelSnap = false;
        [SerializeField] private int pixelsPerUnit = 100; // ваш PPU спрайтів
        [Tooltip("Використовувати саме ЗАСНЕПАНУ позицію камери для розрахунку паралакса (зменшує дьоргання)")]
        [SerializeField] private bool useSnappedCameraForParallax = true;

        [Header("Optional Infinite X Loop")]
        [SerializeField] private bool infiniteX = false;
        [SerializeField] private float tileWidth = 20f; // ширина повторюваного сегмента (в world units)
        [Tooltip("Авто визначити tileWidth зі SpriteRenderer (ширина bounds.x)")]
        [SerializeField] private bool autoDetectTileWidth = false;

        private Vector3 _startLayerPos;
        private Vector3 _startCamPos;
        private Vector3 _currentPos; // для згладження
        private bool _initialized;

        private void OnEnable()
        {
            Init();
        }

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            if (cameraTransform == null) return;

            if (autoDetectTileWidth)
            {
                var sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    tileWidth = sr.bounds.size.x;
                }
            }

            _startLayerPos = transform.position;
            _startCamPos = cameraTransform.position;

            if (pixelSnap && useSnappedCameraForParallax && pixelsPerUnit > 0)
            {
                // Снапимо стартову позицію камери щоб уникнути початкового зсуву
                _startCamPos.x = Mathf.Round(_startCamPos.x * pixelsPerUnit) / pixelsPerUnit;
                _startCamPos.y = Mathf.Round(_startCamPos.y * pixelsPerUnit) / pixelsPerUnit;
            }

            _currentPos = transform.position;
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (cameraTransform == null || !_initialized) return;

            // Визначаємо позицію камери (можливо піксельно-снаплену)
            Vector3 camPos = cameraTransform.position;
            if (pixelSnap && useSnappedCameraForParallax && pixelsPerUnit > 0)
            {
                camPos.x = Mathf.Round(camPos.x * pixelsPerUnit) / pixelsPerUnit;
                camPos.y = Mathf.Round(camPos.y * pixelsPerUnit) / pixelsPerUnit;
                // Коли використовуємо snapped camera – додатковий snap target не потрібен
            }

            // Абсолютне зміщення камери від старту
            Vector3 camOffset = camPos - _startCamPos;

            float targetX = _startLayerPos.x + camOffset.x * (1f - horizontalParallax);
            float targetY;
            if (lockY)
                targetY = _startLayerPos.y; // ігноруємо вертикальні рухи
            else
                targetY = _startLayerPos.y + camOffset.y * (1f - verticalParallax);

            if (infiniteX && tileWidth > 0.01f)
            {
                // Обгортання; rawParallaxX тепер базується на camOffset (який міг бути snapped)
                float rawParallaxX = camOffset.x * (1f - horizontalParallax);
                float wrapped = Mathf.Repeat(rawParallaxX, tileWidth);
                targetX = _startLayerPos.x + wrapped;
            }

            Vector3 target = new Vector3(targetX, targetY, _startLayerPos.z);

            if (pixelSnap && !useSnappedCameraForParallax && pixelsPerUnit > 0)
            {
                // Режим старого варіанту: снапимо вже фінальну позицію
                target.x = Mathf.Round(target.x * pixelsPerUnit) / pixelsPerUnit;
                target.y = Mathf.Round(target.y * pixelsPerUnit) / pixelsPerUnit;
            }

            if (pixelSnap && useSnappedCameraForParallax)
            {
                // При цьому режимі уникати додаткового плавного lerp – можуть виникати субпіксельні артефакти
                smooth = false;
            }

            if (smooth)
                _currentPos = Vector3.Lerp(_currentPos, target, Time.unscaledDeltaTime * smoothSpeed);
            else
                _currentPos = target;

            transform.position = _currentPos;
        }

        public void SetCamera(Transform cam)
        {
            cameraTransform = cam; Init();
        }
    }
}
