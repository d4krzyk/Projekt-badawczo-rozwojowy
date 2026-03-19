using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

public class MenuController : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    const int SW_MAXIMIZE = 3;

    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
#endif

    enum DisplayModeOption
    {
        Fullscreen = 0,
        Windowed = 1
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject mainMenuPanel;
    public GameObject playMenuPanel;

    public GameObject settingsMenuPanel;

    [Header("Settings")]
    public Toggle toggleGenAISettings;
    public TMP_Dropdown resolutionDropdown;

    const string GenAIEnabledKey = "GenAITexturesEnabled";
    const string DisplayModeKey = "DisplayMode";



    [Header("UI audio")]
    public AudioSource audioSource; // opcjonalnie przypisz w inspectorze
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f,1f)] public float uiVolume = 1f;

    Coroutine pendingDisplayModeRoutine;

    void Awake()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (toggleGenAISettings != null)
        {
            bool enabled = PlayerPrefs.GetInt(GenAIEnabledKey, 1) == 1;
            toggleGenAISettings.isOn = enabled;
            toggleGenAISettings.onValueChanged.AddListener(OnGenAIToggleChanged);
        }

        EnsureResolutionDropdownReference();
        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.RemoveListener(OnResolutionDropdownChanged);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionDropdownChanged);
            InitializeDisplayMode();
        }
        else
        {
            Debug.LogWarning("[MenuController] Resolution dropdown is missing. Fullscreen/windowed mode will not be configurable from Options.");
        }
    }

    void OnDestroy()
    {
        if (toggleGenAISettings != null)
        {
            toggleGenAISettings.onValueChanged.RemoveListener(OnGenAIToggleChanged);
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.RemoveListener(OnResolutionDropdownChanged);
        }

        if (pendingDisplayModeRoutine != null)
        {
            StopCoroutine(pendingDisplayModeRoutine);
            pendingDisplayModeRoutine = null;
        }
    }

    void OnGenAIToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt(GenAIEnabledKey, isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    void EnsureResolutionDropdownReference()
    {
        if (resolutionDropdown != null)
            return;

        if (settingsMenuPanel != null)
            resolutionDropdown = settingsMenuPanel.GetComponentInChildren<TMP_Dropdown>(true);

        if (resolutionDropdown == null)
            resolutionDropdown = FindFirstObjectByType<TMP_Dropdown>(FindObjectsInactive.Include);
    }

    void InitializeDisplayMode()
    {
        int dropdownValue = PlayerPrefs.HasKey(DisplayModeKey)
            ? Mathf.Clamp(PlayerPrefs.GetInt(DisplayModeKey), 0, 1)
            : (IsWindowedMode(Screen.fullScreenMode) ? (int)DisplayModeOption.Windowed : (int)DisplayModeOption.Fullscreen);

        resolutionDropdown.SetValueWithoutNotify(dropdownValue);

        if (PlayerPrefs.HasKey(DisplayModeKey))
        {
            ApplyDisplayMode((DisplayModeOption)dropdownValue, savePreference: false);
        }
    }

    void OnResolutionDropdownChanged(int selectedIndex)
    {
        DisplayModeOption displayMode = selectedIndex == (int)DisplayModeOption.Windowed
            ? DisplayModeOption.Windowed
            : DisplayModeOption.Fullscreen;

        ApplyDisplayMode(displayMode, savePreference: true);
    }

    void ApplyDisplayMode(DisplayModeOption displayMode, bool savePreference)
    {
        Resolution monitorResolution = Screen.currentResolution;

        if (pendingDisplayModeRoutine != null)
        {
            StopCoroutine(pendingDisplayModeRoutine);
            pendingDisplayModeRoutine = null;
        }

        if (displayMode == DisplayModeOption.Windowed)
        {
            Screen.SetResolution(monitorResolution.width, monitorResolution.height, FullScreenMode.Windowed);
            pendingDisplayModeRoutine = StartCoroutine(MaximizeWindowNextFrame());
        }
        else
        {
            Screen.SetResolution(monitorResolution.width, monitorResolution.height, FullScreenMode.ExclusiveFullScreen);
        }

        if (savePreference)
        {
            PlayerPrefs.SetInt(DisplayModeKey, (int)displayMode);
            PlayerPrefs.Save();
        }
    }

    bool IsWindowedMode(FullScreenMode fullScreenMode)
    {
        return fullScreenMode == FullScreenMode.Windowed || fullScreenMode == FullScreenMode.MaximizedWindow;
    }

    IEnumerator MaximizeWindowNextFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr windowHandle = GetActiveWindow();
        if (windowHandle == IntPtr.Zero)
            windowHandle = GetForegroundWindow();

        if (windowHandle != IntPtr.Zero)
            ShowWindow(windowHandle, SW_MAXIMIZE);
#endif

        pendingDisplayModeRoutine = null;
    }

    // metody dostępne z zewnątrz do odtwarzania dźwięków UI
    public void PlayHoverSound()
    {
        if (hoverSound == null || audioSource == null) return;
        audioSource.PlayOneShot(hoverSound, uiVolume);
    }

    public void PlayClickSound()
    {
        if (clickSound == null || audioSource == null) return;
        audioSource.PlayOneShot(clickSound, uiVolume);
    }

    public void ShowPlayMenu()
    {
        mainMenuPanel.SetActive(false);
        playMenuPanel.SetActive(true);
    }

    public void ShowMainMenu()
    {
        playMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void ShowSettingsMenu()
    {
        mainMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(true);
    }
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit (działa tylko w buildzie)");
    }

    
}
