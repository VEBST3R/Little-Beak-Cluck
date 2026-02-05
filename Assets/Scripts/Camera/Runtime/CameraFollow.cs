using UnityEngine;

namespace LittleBeakCluck.CameraSystem
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Follow Settings")]
        [SerializeField] private float followSpeed = 5f; // плавність
        [SerializeField] private Vector3 offset = new Vector3(0, 0, -10f);

        [Header("World Bounds (Auto)")]
        [Tooltip("Якщо вказано, межі камери обчислюються автоматично на основі Box/Any Collider2D bounds. Це зручно при розширенні мапи – просто розтягніть колайдер.")]
        [SerializeField] private Collider2D boundsCollider;
        [SerializeField] private bool useBoundsCollider = true;
        [Tooltip("Додатковий відступ всередину від країв bounds (в world units)")]
        [SerializeField] private float boundsMargin = 0f;
        [Tooltip("Переобчислювати ліміти щокадрово (якщо змінюється aspect/size або рухомі межі)")]
        [SerializeField] private bool recalcEveryFrame = true;

        [Header("Clamp Range X")]
        [SerializeField] private bool useClampX = true;
        [SerializeField] private float minX = -10f;
        [SerializeField] private float maxX = 10f;

        [Header("Optional Y Lock / Range")]
        [SerializeField] private bool lockY = true;
        [SerializeField] private float fixedY = 0f;
        [SerializeField] private float minY = -5f;
        [SerializeField] private float maxY = 5f;

        private Camera _cam;
        // Кешовані обчислені обмеження з урахуванням розміру камери
        private float _computedMinX, _computedMaxX, _computedMinY, _computedMaxY;
        private bool _hasComputedBounds;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null)
            {
                _cam = Camera.main;
            }
            RecomputeBoundsIfNeeded(force: true);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (recalcEveryFrame)
            {
                RecomputeBoundsIfNeeded(force: false);
            }

            Vector3 desired = target.position + offset;

            if (lockY)
            {
                desired.y = fixedY + offset.y;
            }
            else
            {
                if (useBoundsCollider && boundsCollider != null && IsOrtho())
                {
                    EnsureComputedBounds();
                    desired.y = Mathf.Clamp(desired.y, _computedMinY, _computedMaxY);
                }
                else
                {
                    desired.y = Mathf.Clamp(desired.y, minY, maxY);
                }
            }

            // Якщо заданий boundsCollider, пріоритетно використовуємо його для X-обмеження (незалежно від Use Clamp X)
            if (useBoundsCollider && boundsCollider != null && IsOrtho())
            {
                EnsureComputedBounds();
                desired.x = Mathf.Clamp(desired.x, _computedMinX, _computedMaxX);
            }
            else if (useClampX)
            {
                desired.x = Mathf.Clamp(desired.x, minX, maxX);
            }

            float smoothFactor = 1f - Mathf.Exp(-Mathf.Max(0f, followSpeed) * Time.deltaTime);
            Vector3 smoothed = Vector3.Lerp(transform.position, desired, smoothFactor);
            transform.position = smoothed;
        }

        public void SetTarget(Transform newTarget) => target = newTarget;
        public void SetXBounds(float min, float max) { minX = min; maxX = max; }

        public void SetBoundsCollider(Collider2D col)
        {
            boundsCollider = col;
            _hasComputedBounds = false;
            RecomputeBoundsIfNeeded(force: true);
        }

        private bool IsOrtho() => _cam != null && _cam.orthographic;

        private void RecomputeBoundsIfNeeded(bool force)
        {
            if (!useBoundsCollider || boundsCollider == null || !IsOrtho())
            {
                _hasComputedBounds = false;
                return;
            }
            if (_hasComputedBounds && !force) return;

            Bounds b = boundsCollider.bounds;
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            // Дозволений простір для центру камери — враховуємо півширину/піввисоту вьюпорту
            _computedMinX = b.min.x + halfW + boundsMargin;
            _computedMaxX = b.max.x - halfW - boundsMargin;
            _computedMinY = b.min.y + halfH + boundsMargin;
            _computedMaxY = b.max.y - halfH - boundsMargin;

            // Якщо bounds менші ніж екран камери — фіксуємо центр всередині bounds
            if (_computedMinX > _computedMaxX)
            {
                float cx = b.center.x;
                _computedMinX = _computedMaxX = cx;
            }
            if (_computedMinY > _computedMaxY)
            {
                float cy = b.center.y;
                _computedMinY = _computedMaxY = cy;
            }

            _hasComputedBounds = true;
        }

        private void EnsureComputedBounds()
        {
            if (!_hasComputedBounds) RecomputeBoundsIfNeeded(force: true);
        }

        private void OnDrawGizmosSelected()
        {
            if (useBoundsCollider && boundsCollider != null)
            {
                Gizmos.color = Color.yellow;
                var b = boundsCollider.bounds;
                Gizmos.DrawWireCube(b.center, b.size);

                if (_cam != null && _cam.orthographic)
                {
                    float halfH = _cam.orthographicSize;
                    float halfW = halfH * _cam.aspect;
                    // Поточні ліміти
                    float minXG = b.min.x + halfW + boundsMargin;
                    float maxXG = b.max.x - halfW - boundsMargin;
                    float minYG = b.min.y + halfH + boundsMargin;
                    float maxYG = b.max.y - halfH - boundsMargin;

                    // Намалюємо прямокутник можливого центру камери
                    Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
                    Vector3 centerRect = new Vector3((minXG + maxXG) * 0.5f, (minYG + maxYG) * 0.5f, 0f);
                    Vector3 sizeRect = new Vector3(Mathf.Max(0f, maxXG - minXG), Mathf.Max(0f, maxYG - minYG), 0.01f);
                    Gizmos.DrawWireCube(centerRect, sizeRect);
                }
            }
        }
    }
}
