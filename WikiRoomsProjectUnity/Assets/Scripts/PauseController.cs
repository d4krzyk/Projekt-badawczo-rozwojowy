using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class PauseController : MonoBehaviour
{
    public PlayerController playerController;
    public Button resumeButton;
    public Button menuButton;
    public Button giveUpButton;

    [Header("UI Sounds")]
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float soundVolume = 1f;
    
    AudioSource audioSource;
    CanvasGroup canvasGroup;

    public void Start()
    {
        // Przygotuj AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        // Przygotuj CanvasGroup do ukrywania UI bez wyłączania animacji
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Znajdź PlayerController jeśli nie jest przypisany
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        // Przypisz callback do przycisków
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(OnResumeClicked);
            AddHoverSoundToButton(resumeButton);
        }

        if (menuButton != null)
        {
            menuButton.onClick.AddListener(OnMenuClicked);
            AddHoverSoundToButton(menuButton);
        }

        if (giveUpButton != null)
        {
            giveUpButton.onClick.AddListener(OnGiveUpClicked);
            AddHoverSoundToButton(giveUpButton);
        }
    }

    public void PlayHoverSound()
    {
        if (playerController != null)
            playerController.PlayUISound(hoverSound, soundVolume);
        else if (audioSource != null && hoverSound != null)
            audioSource.PlayOneShot(hoverSound, soundVolume);
    }

    public void PlayClickSound()
    {
        if (playerController != null)
            playerController.PlayUISound(clickSound, soundVolume);
        else if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound, soundVolume);
    }

    public void OnResumeClicked()
    {
        
        if (playerController != null)
        {
            playerController.TogglePause();
            Time.timeScale = 1f;
            PlayClickSound();
            Debug.Log("Resume button clicked");
        }
    }

    public void OnMenuClicked()
    {
        Debug.Log("Menu button clicked");
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
        PlayClickSound();
    }

    public void OnGiveUpClicked()
    {
        Debug.Log("Give up button clicked");
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
        PlayClickSound();
    }

    void AddHoverSoundToButton(Button button)
    {
        // Dodaj dźwięk na hover za pomocą EventTrigger
        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        // Dodaj event dla wjechania myszą
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener((data) => PlayHoverSound());
        trigger.triggers.Add(entry);
    }

}
