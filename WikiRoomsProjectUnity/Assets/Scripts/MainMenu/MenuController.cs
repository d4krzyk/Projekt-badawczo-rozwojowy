using UnityEngine;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject mainMenuPanel;
    public GameObject playMenuPanel;

    public GameObject settingsMenuPanel;

    [Header("Settings")]
    public Toggle toggleGenAISettings;

    const string GenAIEnabledKey = "GenAITexturesEnabled";



    [Header("UI audio")]
    public AudioSource audioSource; // opcjonalnie przypisz w inspectorze
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f,1f)] public float uiVolume = 1f;

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
    }

    void OnDestroy()
    {
        if (toggleGenAISettings != null)
        {
            toggleGenAISettings.onValueChanged.RemoveListener(OnGenAIToggleChanged);
        }
    }

    void OnGenAIToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt(GenAIEnabledKey, isOn ? 1 : 0);
        PlayerPrefs.Save();
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
