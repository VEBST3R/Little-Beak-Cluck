using System.Collections.Generic;
using Lean.Pool;
using LittleBeakCluck.Enemies;
using UnityEngine;

namespace LittleBeakCluck.UI
{
    /// <summary>
    /// Centralised controller that keeps track of all enemies and renders either an on-screen bar or an off-screen arrow.
    /// Attach this to a dedicated Canvas (Screen Space) and assign the camera references in inspector.
    /// </summary>
    public class EnemyHudController : MonoBehaviour
    {
        private static EnemyHudController _instance;

        [Header("Canvas References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private Camera _uiCamera;
        [SerializeField] private RectTransform _barContainer;
        [SerializeField] private RectTransform _arrowContainer;

        [Header("Prefabs & Pooling")]
        [SerializeField] private EnemyHudBar _barPrefab;
        [SerializeField] private EnemyHudArrow _arrowPrefab;

        [Header("Behaviour")]
        [SerializeField] private float _screenEdgePadding = 32f;
        [SerializeField] private float _arrowVerticalOffset = 0f;
        [SerializeField] private Color _defaultBarColor = Color.red;
        [SerializeField] private Color _defaultArrowColor = Color.white;

        private readonly List<Entry> _entries = new();
        private readonly Queue<Entry> _entriesToRemove = new();
        private RectTransform _canvasRect;

        private class Entry
        {
            public EnemyBehaviour Enemy;
            public EnemyHudBar Bar;
            public EnemyHudArrow Arrow;
            public Vector3 BarOffset;
            public Vector3 ArrowOffset;
            public bool Subscribed;
            public float LastFill = 1f;

            public void Cleanup()
            {
                if (Enemy != null)
                {
                    Enemy.OnHealthChanged -= HandleHealth;
                    Enemy.OnDied -= HandleDeath;
                }
                Enemy = null;
                Subscribed = false;
            }

            public void HandleHealth(float current, float max)
            {
                if (Bar == null) return;
                float fill = max > 0.001f ? current / max : 0f;
                LastFill = fill;
                Bar.SetFill(fill);
            }

            public void HandleDeath()
            {
                var instance = EnemyHudController.Instance;
                if (instance != null && instance._entries != null)
                {
                    instance.QueueRemoval(this);
                }
            }
        }

        public static EnemyHudController Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("Multiple EnemyHudController instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();
            if (_worldCamera == null)
                _worldCamera = Camera.main;

            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : transform as RectTransform;
            if (_barContainer == null)
                _barContainer = _canvasRect;
            if (_arrowContainer == null)
                _arrowContainer = _canvasRect;

            if (_canvasRect == null)
            {
                Debug.LogError("EnemyHudController requires a RectTransform canvas root.", this);
            }

            var existing = FindObjectsByType<EnemyBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                RegisterInternal(existing[i]);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void LateUpdate()
        {
            ProcessRemovals();

            if (_entries.Count == 0)
                return;

            if (_worldCamera == null)
                _worldCamera = Camera.main;

            Camera uiCamera = _uiCamera != null ? _uiCamera : (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null);
            Rect rect = _canvasRect.rect;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.Enemy == null || !entry.Enemy.isActiveAndEnabled)
                {
                    QueueRemoval(entry);
                    continue;
                }

                Vector3 barWorldPos = entry.Enemy.transform.position + entry.BarOffset;
                Vector3 arrowWorldPos = entry.Enemy.transform.position + entry.ArrowOffset;
                Vector3 viewport = _worldCamera != null ? _worldCamera.WorldToViewportPoint(arrowWorldPos) : Vector3.zero;
                bool inFront = viewport.z > 0f;
                bool inside = inFront && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;

                if (inside)
                {
                    Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(_worldCamera, barWorldPos);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, uiCamera, out Vector2 localPoint))
                    {
                        entry.Bar.RectTransform.localPosition = localPoint;
                        entry.Bar.SetFill(entry.LastFill);
                        entry.Bar.SetActive(true);
                        entry.Arrow.SetActive(false);
                    }
                }
                else
                {
                    RectTransform targetRect = entry.Arrow.RectTransform.parent as RectTransform ?? _arrowContainer ?? _canvasRect;
                    Rect rectBounds = targetRect.rect;
                    Vector2 half = rectBounds.size * 0.5f;
                    half.x = Mathf.Max(0f, half.x - _screenEdgePadding);
                    half.y = Mathf.Max(0f, half.y - _screenEdgePadding);

                    if (half.x < 0.001f || half.y < 0.001f)
                    {
                        entry.Arrow.RectTransform.localPosition = Vector2.zero;
                        entry.Arrow.SetRotation(0f);
                        entry.Arrow.SetActive(true);
                        entry.Bar.SetActive(false);
                        continue;
                    }

                    Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(_worldCamera, arrowWorldPos);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, screenPos, uiCamera, out Vector2 localFromCenter);

                    if (!inFront)
                    {
                        localFromCenter = -localFromCenter;
                    }

                    Vector2 viewportOffset = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                    if (!inFront)
                    {
                        viewportOffset = -viewportOffset;
                    }

                    if (viewportOffset.sqrMagnitude < 0.0001f)
                    {
                        viewportOffset = Vector2.up * 0.5f;
                    }

                    bool horizontalPriority = Mathf.Abs(viewportOffset.x) >= Mathf.Abs(viewportOffset.y);
                    Vector2 bounded;
                    if (horizontalPriority)
                    {
                        float x = viewportOffset.x >= 0f ? half.x : -half.x;
                        float y = Mathf.Clamp(localFromCenter.y, -half.y, half.y);
                        bounded = new Vector2(x, y);
                    }
                    else
                    {
                        float y = viewportOffset.y >= 0f ? half.y : -half.y;
                        float x = Mathf.Clamp(localFromCenter.x, -half.x, half.x);
                        bounded = new Vector2(x, y);
                    }

                    Vector2 dir = bounded.normalized;
                    if (dir.sqrMagnitude < 0.0001f)
                    {
                        dir = Vector2.up;
                    }

                    entry.Arrow.RectTransform.localPosition = bounded;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    entry.Arrow.SetRotation(angle - 90f);
                    entry.Arrow.SetActive(true);
                    entry.Bar.SetActive(false);
                }
            }
        }

        private void ProcessRemovals()
        {
            while (_entriesToRemove.Count > 0)
            {
                var entry = _entriesToRemove.Dequeue();
                if (entry == null) continue;
                entry.Cleanup();
                if (entry.Bar != null)
                {
                    LeanPool.Despawn(entry.Bar.gameObject);
                    entry.Bar = null;
                }
                if (entry.Arrow != null)
                {
                    LeanPool.Despawn(entry.Arrow.gameObject);
                    entry.Arrow = null;
                }
                _entries.Remove(entry);
            }
        }

        private void QueueRemoval(Entry entry)
        {
            if (entry == null) return;
            if (!_entriesToRemove.Contains(entry))
                _entriesToRemove.Enqueue(entry);
        }

        public static void Register(EnemyBehaviour enemy)
        {
            if (enemy == null || Instance == null)
                return;

            Instance.RegisterInternal(enemy);
        }

        public static void Unregister(EnemyBehaviour enemy)
        {
            if (enemy == null || Instance == null)
                return;

            Instance.UnregisterInternal(enemy);
        }

        private void RegisterInternal(EnemyBehaviour enemy)
        {
            if (enemy == null)
                return;

            if (_entries.Exists(e => e.Enemy == enemy))
                return;

            if (_barPrefab == null || _arrowPrefab == null)
            {
                Debug.LogError("EnemyHudController requires bar and arrow prefabs assigned.", this);
                return;
            }

            var barGo = LeanPool.Spawn(_barPrefab, _barContainer);
            var arrowGo = LeanPool.Spawn(_arrowPrefab, _arrowContainer);

            if (barGo != null)
            {
                barGo.RectTransform.anchoredPosition = Vector2.zero;
                barGo.RectTransform.localScale = Vector3.one;
            }

            if (arrowGo != null)
            {
                arrowGo.RectTransform.anchoredPosition = Vector2.zero;
                arrowGo.RectTransform.localScale = Vector3.one;
            }

            barGo.SetActive(false);
            arrowGo.SetActive(false);

            var entry = new Entry
            {
                Enemy = enemy,
                Bar = barGo,
                Arrow = arrowGo,
                BarOffset = enemy.HudWorldOffset,
                ArrowOffset = new Vector3(0f, _arrowVerticalOffset, 0f)
            };

            enemy.OnHealthChanged += entry.HandleHealth;
            enemy.OnDied += entry.HandleDeath;
            entry.Subscribed = true;

            Sprite waveIcon = enemy.Config != null ? enemy.Config.waveTypeIcon : null;
            Color barColor = enemy.Config != null ? enemy.Config.hudBarColor : _defaultBarColor;
            Color arrowColor = enemy.Config != null && enemy.Config.hudArrowColor.a > 0f ? enemy.Config.hudArrowColor : _defaultArrowColor;

            entry.Bar.SetWaveSprite(waveIcon);
            entry.Bar.SetBarColor(barColor);
            entry.Arrow.SetColor(arrowColor);
            entry.HandleHealth(enemy.CurrentHealth, enemy.MaxHealth);

            _entries.Add(entry);
        }

        private void UnregisterInternal(EnemyBehaviour enemy)
        {
            if (enemy == null)
                return;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Enemy == enemy)
                {
                    QueueRemoval(_entries[i]);
                    break;
                }
            }
        }
    }
}
