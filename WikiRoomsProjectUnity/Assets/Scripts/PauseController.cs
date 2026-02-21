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
    public GameObject finalUI;
    public TMPro.TMP_Text finalUIScore;
    public TMPro.TMP_Text finalUIRoomCount;
    public TMPro.TMP_Text finalUITime;

    public Logger logger;

    [Header("UI Sounds")]
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float soundVolume = 1f;
    AudioSource audioSource;
    CanvasGroup canvasGroup;
    bool sessionEnded;

    public void Start()
    {
        AudioListener.pause = false;

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
            playerController = FindAnyObjectByType<PlayerController>();
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
        AudioListener.pause = false;
        Time.timeScale = 1f;
        Destroy(FindAnyObjectByType<GameController>());
        SceneManager.LoadScene("MainMenu");
        PlayClickSound();
    }

    public void OnGiveUpClicked()
    {
        if (sessionEnded) return;
        sessionEnded = true;

        Debug.Log("Give up button clicked");
        PlayClickSound();
        EnterFinalState();
        finalUI.SetActive(true);
        finalUIScore.text = logger.GetTotalBooksOpened().ToString("D9");
        finalUIRoomCount.text = logger.GetTotalRoomsVisited().ToString("D9");
        int duration = (int)logger.GetSessionDuration();
        int hours = Mathf.FloorToInt(duration / 3600);
        int minutes = Mathf.FloorToInt(duration / 60 % 60);
        int seconds = Mathf.FloorToInt(duration % 60);
        finalUITime.text = $"{hours:00} h {minutes:00} min {seconds:00} s";
        logger.SendLogs();
    }

    void EnterFinalState()
    {
        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        if (playerController != null)
        {
            playerController.movementLocked = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
