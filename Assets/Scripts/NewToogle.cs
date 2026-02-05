using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

public class NewToggle : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private Animator animator;
    [SerializeField] private string parameterName = "isOn";
    [SerializeField] private bool persistState = true;
    [SerializeField] private string playerPrefsKey = string.Empty;

    private int _parameterHash;
    private string _resolvedPrefsKey;
    private bool _initialized;
    private bool _readyForImmediateUpdate;

    private void Awake()
    {
        if (!EnsureInitialized())
            return;
        // Defer animator immediate update to Start to avoid Unity internal 'm_DidAwake' assertion.
        // Persisted state is already applied in TryInitialize -> LoadPersistedState.
    }

    private void Start()
    {
        _readyForImmediateUpdate = true;
        if (_initialized)
        {
            // Now it's safe to force an immediate animator evaluation if needed.
            UpdateAnimator(toggle.isOn, true);
            PersistState(toggle.isOn);
        }
    }

    private void OnEnable()
    {
        if (!EnsureInitialized())
            return;

        toggle.onValueChanged.AddListener(OnToggleValueChanged);
        UpdateAnimator(toggle.isOn, true);
    }

    private void OnDisable()
    {
        if (!_initialized || toggle == null)
            return;

        toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
    }

    private bool EnsureInitialized()
    {
        if (_initialized)
            return true;

        if (!TryInitialize())
            return false;

        _initialized = true;
        return true;
    }

    private bool TryInitialize()
    {
        if (toggle == null)
        {
            toggle = GetComponent<Toggle>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (toggle == null)
        {
            Debug.LogWarning($"[{name}] NewToggle missing Toggle component.", this);
            enabled = false;
            return false;
        }

        if (animator == null)
        {
            Debug.LogWarning($"[{name}] NewToggle missing Animator component.", this);
            enabled = false;
            return false;
        }

        if (string.IsNullOrEmpty(parameterName))
        {
            parameterName = "isOn";
        }

        _parameterHash = Animator.StringToHash(parameterName);
        _resolvedPrefsKey = persistState ? ResolvePrefsKey() : null;

        LoadPersistedState();
        return true;
    }

    private void OnToggleValueChanged(bool isOn)
    {
        if (!_initialized || toggle == null || animator == null)
            return;

        UpdateAnimator(isOn);
        PlayToggleSound();
        PersistState(isOn);
    }

    private void UpdateAnimator(bool isOn, bool forceImmediate = false)
    {
        if (animator == null)
            return;

        animator.SetBool(_parameterHash, isOn);

        if (forceImmediate && _readyForImmediateUpdate && animator.isActiveAndEnabled)
        {
            animator.Update(0f);
        }
    }

    public void RefreshState()
    {
        if (!EnsureInitialized())
            return;

        if (toggle != null)
        {
            UpdateAnimator(toggle.isOn, true);
            PersistState(toggle.isOn);
        }
    }

    private void LoadPersistedState()
    {
        if (toggle == null || string.IsNullOrEmpty(_resolvedPrefsKey))
            return;

        bool stored = PlayerPrefs.GetInt(_resolvedPrefsKey, toggle.isOn ? 1 : 0) != 0;
        toggle.SetIsOnWithoutNotify(stored);
    }

    private void PersistState(bool isOn)
    {
        if (string.IsNullOrEmpty(_resolvedPrefsKey))
            return;

        PlayerPrefs.SetInt(_resolvedPrefsKey, isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private string ResolvePrefsKey()
    {
        if (!string.IsNullOrEmpty(playerPrefsKey))
            return playerPrefsKey;

        var scene = gameObject.scene;
        string sceneName = scene.IsValid() ? scene.name : "Global";
        return $"LBC_Toggle_{sceneName}_{name}";
    }

    private static void PlayToggleSound()
    {
        var audio = ServiceLocator.Instance.Get<IAudioService>();
        audio?.PlayUiClick();
    }
}