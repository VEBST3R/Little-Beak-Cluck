using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.Player;
using LittleBeakCluck.Services;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    [Preserve]
    [Obfuscation(Feature = "rename", Exclude = true, ApplyToMembers = true)]
    public class AttackButtonHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private PlayerAttack playerAttack;
        [SerializeField] private bool logDebug;
        [SerializeField] private bool ensureRaycastTarget = true;
        [Tooltip("If enabled, will automatically add InputSystemUIInputModule to the EventSystem at runtime when using the new Input System. Useful for iOS where touches won't be delivered without it.")]
        [SerializeField] private bool autoFixEventSystemForInputSystem = true;

        private bool _pressed;

        private void Awake()
        {
            ValidateRaycastTarget();
            EnsureGraphicRaycasterPresent();
            TryResolvePlayerAttack();
            WarnIfEventSystemModuleMissing();
            EnsureGuiLogOverlay();
        }

        private void OnEnable()
        {
            if (playerAttack == null)
                TryResolvePlayerAttack();
        }

        private void EnsureGraphicRaycasterPresent()
        {
            // iOS often requires a proper GraphicRaycaster on the Canvas for touches
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.isActiveAndEnabled)
            {
                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                    if (logDebug)
                        Debug.Log("[AttackButtonHandler] Added GraphicRaycaster to parent Canvas.", canvas);
                }
            }
        }

        private void TryResolvePlayerAttack()
        {
            if (playerAttack != null)
                return;

            // Prefer an active & enabled PlayerAttack in the active scene.
#if UNITY_2022_2_OR_NEWER
            var all = FindObjectsByType<PlayerAttack>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var all = FindObjectsOfType<PlayerAttack>(true);
#endif
            PlayerAttack pick = null;
            foreach (var pa in all)
            {
                if (pa != null && pa.isActiveAndEnabled)
                {
                    pick = pa;
                    break;
                }
            }
            // If none are active, fall back to any found (but warn).
            if (pick == null && all != null && all.Length > 0)
            {
                pick = all[0];
                if (logDebug && pick != null)
                {
                    Debug.LogWarning($"[AttackButtonHandler] Found PlayerAttack but it's not active/enabled (name='{pick.name}', activeInHierarchy={pick.gameObject.activeInHierarchy}, enabled={pick.enabled}). Using it anyway.", pick);
                }
            }

            playerAttack = pick;
            if (logDebug)
            {
                if (playerAttack == null)
                {
                    Debug.LogWarning("[AttackButtonHandler] PlayerAttack not found in scene.", this);
                }
                else
                {
                    Debug.Log($"[AttackButtonHandler] Bound to PlayerAttack name='{playerAttack.name}', activeInHierarchy={playerAttack.gameObject.activeInHierarchy}, enabled={playerAttack.enabled}.", playerAttack);
                }
            }
        }

        private void ValidateRaycastTarget()
        {
            var g = GetComponent<Graphic>();
            if (g == null)
            {
                if (logDebug)
                    Debug.LogWarning("[AttackButtonHandler] No Graphic on this object. Attempting to forward events from first child Graphic.", this);

                // If this object has no Graphic, try attach a forwarder to the first child Graphic
                var childGraphic = GetComponentInChildren<Graphic>();
                if (childGraphic != null && childGraphic.gameObject != gameObject)
                {
                    var forwarder = childGraphic.gameObject.GetComponent<PointerForwarder>();
                    if (forwarder == null)
                    {
                        forwarder = childGraphic.gameObject.AddComponent<PointerForwarder>();
                    }
                    forwarder.target = this;
                    if (ensureRaycastTarget && !childGraphic.raycastTarget)
                    {
                        childGraphic.raycastTarget = true;
                    }
                }
                return;
            }
            if (ensureRaycastTarget && !g.raycastTarget)
            {
                g.raycastTarget = true;
                if (logDebug)
                    Debug.Log("[AttackButtonHandler] Enabled raycastTarget on Graphic.", this);
            }
        }

        private void WarnIfEventSystemModuleMissing()
        {
            var es = EventSystem.current;
            if (es == null)
            {
                if (autoFixEventSystemForInputSystem)
                {
                    // Create minimal EventSystem and attach suitable input module
                    var go = new GameObject("EventSystem");
                    es = go.AddComponent<EventSystem>();
                    bool addedNewInput = TryAddInputSystemModuleReflective(es);
                    if (!addedNewInput)
                    {
                        go.AddComponent<StandaloneInputModule>();
                    }
                    if (logDebug)
                        Debug.Log("[AttackButtonHandler] Created EventSystem at runtime and attached input module.", go);
                }
                else if (logDebug)
                {
                    Debug.LogWarning("[AttackButtonHandler] No EventSystem in scene. UI won't receive touches.", this);
                }
                return;
            }
            // Try to ensure the new Input System UI module exists even if scripting defines differ between platforms
            if (autoFixEventSystemForInputSystem)
            {
                try
                {
                    TryAddInputSystemModuleReflective(es);
                }
                catch (Exception ex)
                {
                    if (logDebug)
                        Debug.LogWarning($"[AttackButtonHandler] Failed to auto-fix EventSystem: {ex.Message}", es);
                }
            }
        }

        private bool TryAddInputSystemModuleReflective(EventSystem es)
        {
#if ENABLE_INPUT_SYSTEM
            var inputModuleType = typeof(InputSystemUIInputModule);
#else
            var inputModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem.UI");
            if (inputModuleType == null)
            {
                inputModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            }
#endif
            if (inputModuleType == null)
                return false;

            var existing = es.GetComponent(inputModuleType);
            if (existing != null)
            {
                TryAssignDefaultActions(existing);
                return true;
            }

            var added = es.gameObject.AddComponent(inputModuleType);
            TryAssignDefaultActions(added);

            if (logDebug)
                Debug.Log("[AttackButtonHandler] Added InputSystemUIInputModule via reflection.", es);

            return true;
        }

        private static void TryAssignDefaultActions(Component moduleComponent)
        {
            if (moduleComponent == null)
                return;

#if ENABLE_INPUT_SYSTEM
            if (moduleComponent is InputSystemUIInputModule typedModule)
            {
                ConfigureActionsAsset(typedModule);
                var assignMethod = typeof(InputSystemUIInputModule).GetMethod("AssignDefaultActions", BindingFlags.Public | BindingFlags.Instance);
                if (assignMethod != null)
                {
                    assignMethod.Invoke(typedModule, null);
                }
                return;
            }
#endif

            ConfigureActionsAssetReflective(moduleComponent);
            var method = moduleComponent.GetType().GetMethod("AssignDefaultActions", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(moduleComponent, null);
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static void ConfigureActionsAsset(InputSystemUIInputModule module)
        {
            if (module == null)
                return;

            var locator = ServiceLocator.Instance;
            var inputService = locator.Get<IInputService>();
            if (inputService?.ActionsAsset != null)
            {
                module.actionsAsset = inputService.ActionsAsset;
            }
        }
#else
        private static void ConfigureActionsAsset(InputSystemUIInputModule module) { }
#endif

        private static void ConfigureActionsAssetReflective(Component moduleComponent)
        {
            if (moduleComponent == null)
                return;

            var locator = ServiceLocator.Instance;
            var inputService = locator.Get<IInputService>();
            var asset = inputService?.ActionsAsset;
            if (asset == null)
                return;

            var property = moduleComponent.GetType().GetProperty("actionsAsset", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(moduleComponent, asset);
            }
        }

        // Helper component to forward pointer events from a child Graphic to this handler
        [Preserve]
        private class PointerForwarder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public AttackButtonHandler target;
            public void OnPointerDown(PointerEventData eventData) => target?.OnPointerDown(eventData);
            public void OnPointerUp(PointerEventData eventData) => target?.OnPointerUp(eventData);
            public void OnPointerExit(PointerEventData eventData) => target?.OnPointerExit(eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            if (playerAttack == null)
                TryResolvePlayerAttack();
            if (logDebug)
            {
                string state = playerAttack != null ? $"boundTo='{playerAttack.name}', active={playerAttack.gameObject.activeInHierarchy}, enabled={playerAttack.enabled}" : "NO_PLAYER_ATTACK";
                Debug.Log($"[AttackButtonHandler] PointerDown -> StartCharging() ({state})", this);
                playerAttack?.EnableDebugLogging(true);
            }
            playerAttack?.StartCharging();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressed)
                return;
            _pressed = false;
            if (playerAttack == null)
                TryResolvePlayerAttack();
            if (logDebug)
            {
                string state = playerAttack != null ? $"boundTo='{playerAttack.name}', active={playerAttack.gameObject.activeInHierarchy}, enabled={playerAttack.enabled}" : "NO_PLAYER_ATTACK";
                Debug.Log($"[AttackButtonHandler] PointerUp -> ReleaseAttack() ({state})", this);
                playerAttack?.EnableDebugLogging(true);
            }
            playerAttack?.ReleaseAttack();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_pressed)
                return;
            _pressed = false;
            if (playerAttack == null)
                TryResolvePlayerAttack();
            if (logDebug)
            {
                string state = playerAttack != null ? $"boundTo='{playerAttack.name}', active={playerAttack.gameObject.activeInHierarchy}, enabled={playerAttack.enabled}" : "NO_PLAYER_ATTACK";
                Debug.Log($"[AttackButtonHandler] PointerExit -> ReleaseAttack() ({state})", this);
            }
            playerAttack?.ReleaseAttack();
        }

        private void EnsureGuiLogOverlay()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!logDebug)
                return;
            GuiLogOverlay.Enable();
#endif
        }

        private static class GuiLogOverlay
        {
            private const int MaxLines = 12;
            private static OverlayBehaviour _instance;

            public static void Enable()
            {
                if (_instance != null)
                    return;

                var go = new GameObject("GuiLogOverlay");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                _instance = go.AddComponent<OverlayBehaviour>();
            }

            private class OverlayBehaviour : MonoBehaviour
            {
                private readonly Queue<string> _lines = new();
                private string _combined = string.Empty;
                private GUIStyle _style;
                private Rect _rect;
                private StringBuilder _builder;
                private static readonly string[] s_FilterTokens =
                {
                    "AttackButtonHandler",
                    "PlayerAttack",
                    "StartCharging",
                    "ReleaseAttack",
                    "PointerDown",
                    "PointerUp",
                    "Attack"
                };

                private void OnEnable()
                {
                    Application.logMessageReceived += HandleLog;
                }

                private void OnDisable()
                {
                    Application.logMessageReceived -= HandleLog;
                }

                private void HandleLog(string condition, string stackTrace, LogType type)
                {
                    if (!ShouldCapture(condition))
                        return;

                    _lines.Enqueue($"[{type}] {condition}");
                    while (_lines.Count > MaxLines)
                        _lines.Dequeue();

                    _builder ??= new StringBuilder(256);
                    _builder.Clear();
                    foreach (var line in _lines)
                    {
                        _builder.AppendLine(line);
                    }
                    _combined = _builder.ToString();
                }

                private void OnGUI()
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (string.IsNullOrEmpty(_combined))
                        return;

                    if (_style == null)
                    {
                        _style = new GUIStyle(GUI.skin.box)
                        {
                            alignment = TextAnchor.UpperLeft,
                            fontSize = 24,
                            wordWrap = true
                        };
                        _style.normal.textColor = Color.white;
                    }

                    if (_rect.width <= 0f)
                    {
                        float width = Mathf.Clamp(Screen.width * 0.6f, 240f, 560f);
                        // Make the overlay taller on screen
                        float height = Mathf.Clamp(Screen.height * 0.55f, 220f, 560f);
                        _rect = new Rect(16f, 16f, width, height);
                    }

                    GUI.Box(_rect, _combined, _style);
#endif
                }

                private static bool ShouldCapture(string message)
                {
                    if (string.IsNullOrEmpty(message))
                        return false;

                    foreach (string token in s_FilterTokens)
                    {
                        if (message.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }

                    return false;
                }
            }
        }
    }
}
