using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using LogicUI.FancyTextRendering;

public class CursorController : MonoBehaviour
{
    [Header("Crosshair Settings")]
    public Image crosshairImage;
    public Sprite normalCrosshair;
    public Sprite highlightedCrosshair;

    [Header("Raycast Settings")]
    public Camera mainCamera;
    public float rayDistance = 1f;

    [Header("Audio")]
    public AudioClip hoverSound;
    [Range(0f,1f)] public float hoverVolume = 0.5f;
    public AudioSource audioSource;
    public PlayerController playerController;

    // pole śledzące ostatnio najechany obiekt
    GameObject lastHoveredObject;

    // flaga informująca, czy dla aktualnie najechanego obiektu już zagrano dźwięk
    bool hoverSoundPlayed = false;

    // Outline
    [Header("Outline Settings")]
    public Color outlineColor = Color.yellow;
    [Range(0f, 10f)] public float outlineWidth = 4f;
    public bool addOutlineIfMissing = true; // doda Outline gdy brak

    Outline lastOutline; // aktualny outline na obiekcie

    [Header("Hover UI")]
    public GameObject hoverUI; // przypisz GameObject z GUI (wyłączony domyślnie)
    public string[] hoverTags = new string[] { "Book", "Image", "PortalBack", "PortalNext", "InfoBox" };

    // referencje do tekstu w hoverUI (przypisz w Inspektorze lub zostaną znalezione automatycznie)
    public MarkdownRenderer hoverText;

    // referencja do RoomsController (przypisz w Inspektorze lub znajdzie automatycznie)
    public RoomsController roomsController;

    // Hover UI Interactivity - pozwala na klikanie w tekst w hover UI gdy wciśnięty jest Ctrl
    private bool isHoverUIInteractive = false; // flaga czy hover UI jest w trybie interaktywnym
    private GameObject currentImageObject = null; // przechowuje aktualnie najechany Image do sprawdzenia Ctrl

    void Start()
    {
        if (crosshairImage == null)
            Debug.LogError("Crosshair Image not assigned!");
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (crosshairImage != null)
            crosshairImage.raycastTarget = false;

        Cursor.visible = false;
        if (crosshairImage != null)
            crosshairImage.sprite = normalCrosshair;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound (możesz ustawić 3D jeśli chcesz)
        }

        if (hoverUI != null)
            hoverUI.SetActive(false);

    }

    void Update()
    {
        // Sprawdź czy Ctrl jest wciśnięty, a Image jest najechany
        if (currentImageObject != null && Input.GetKey(KeyCode.LeftControl))
        {
            // Włącz tryb interaktywny dla hover UI
            if (!isHoverUIInteractive)
            {
                isHoverUIInteractive = true;
                // Pokaż kursor i zablokuj jego ruchy
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                // Zablokuj ruch gracza
                if (playerController != null)
                    playerController.movementLocked = true;
                Debug.Log("[CursorController] Hover UI is now interactive (Ctrl pressed)");
            }
        }
        else
        {
            // Wyłącz tryb interaktywny jeśli Ctrl nie jest wciśnięty
            if (isHoverUIInteractive)
            {
                isHoverUIInteractive = false;
                // Ukryj kursor i zablokuj go
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                // Odblokuj ruch gracza
                if (playerController != null)
                    playerController.movementLocked = false;
                Debug.Log("[CursorController] Hover UI interactive mode disabled");
            }
        }
    }


    void LateUpdate()
    {
        UpdateCrosshair();
    }

    void UpdateCrosshair()
    {
        if (mainCamera == null)
            return;

        float effectiveDistance = (playerController != null) ? playerController.interactDistance : rayDistance;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float startOffset = mainCamera.nearClipPlane + 0.005f;
        Vector3 origin = ray.origin + ray.direction * startOffset;
        ray = new Ray(origin, ray.direction);
        float castDistance = effectiveDistance + startOffset;

        Debug.DrawRay(ray.origin, ray.direction * castDistance, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, castDistance))
        {
            Debug.DrawLine(ray.origin, hit.point, Color.green);
            float distanceFromCamera = Vector3.Distance(mainCamera.transform.position, hit.point);

            string hitTag = hit.collider.tag;
            bool isHoverTag = false;
            foreach (var t in hoverTags)
            {
                if (hitTag == t) { isHoverTag = true; break; }
            }

            if (isHoverTag && distanceFromCamera <= effectiveDistance)
            {
                GameObject hitObj = hit.collider.gameObject;

                if (hitObj != lastHoveredObject)
                {
                    // wyłącz outline z poprzedniego obiektu
                    DisableLastOutline();

                    lastHoveredObject = hitObj;
                    hoverSoundPlayed = false;
                }

                // jeśli to Book — pobierz bookName i pokaż w hoverUI
                if (hitTag == "Book")
                {
                    string bookName = null;
                    var bookComp = hitObj.GetComponent<BookInteraction>();
                    if (bookComp == null && hitObj.transform.parent != null)
                        bookComp = hitObj.transform.parent.GetComponent<BookInteraction>();

                    if (bookComp != null)
                        bookName = bookComp.bookName;

                    // włącz outline i dźwięk (o ile nie Portal — tu jest Book więc dźwięk normalnie)
                    if (!hoverSoundPlayed)
                    {
                        EnsureAndEnableOutline(hitObj);
                        PlaySound();
                        hoverSoundPlayed = true;
                    }

                    SetCrosshair(highlightedCrosshair);
                    ShowHoverUI(bookName);
                    return;
                }

                // jeśli to Portal — pobierz nazwę pokoju z RoomsController i pokaz
                if (hitTag == "PortalBack" || hitTag == "PortalNext")
                {
                    string roomName = null;
                    if (roomsController != null)
                    {
                        if (hitTag == "PortalBack")
                            roomName = roomsController.GetPreviousRoomName();
                        else // PortalNext
                            roomName = roomsController.GetNextRoomName();
                    }
                    string displayRoomName = !string.IsNullOrEmpty(roomName) ? Uri.UnescapeDataString(roomName) : string.Empty;   
                    // nie odtwarzaj dźwięku dla portalu
                    hoverSoundPlayed = true;

                    // włącz outline (opcjonalnie)
                    EnsureAndEnableOutline(hitObj);

                    SetCrosshair(highlightedCrosshair);
                    ShowHoverUI(!string.IsNullOrEmpty(displayRoomName) ? "Portal to " + displayRoomName : string.Empty);
                    return;
                }

                // InfoBox: pokaż zawartość (jeśli dostępna) i odtwórz dźwięk hover
                if (hitTag == "InfoBox")
                {
                    var infoComp = hitObj.GetComponent<InfoBoxInteraction>();
                    if (!hoverSoundPlayed)
                    {
                        EnsureAndEnableOutline(hitObj);
                        PlaySound();
                        hoverSoundPlayed = true;
                    }

                    SetCrosshair(highlightedCrosshair);
                    ShowHoverUI(!string.IsNullOrEmpty(roomsController.elongatedRoom.articleName) ? "Info about " + roomsController.elongatedRoom.articleName : string.Empty);
                    return;
                }

                // jeśli to Image — pokaż caption (jeśli dostępne)
                if (hitTag == "Image")
                {
                    string caption = null;
                    var imgComp = hitObj.GetComponent<ImageInteraction>();
                    if (imgComp == null && hitObj.transform.parent != null)
                        imgComp = hitObj.transform.parent.GetComponent<ImageInteraction>();

                    caption = imgComp?.caption;
                    
                    // Zapamiętaj aktualny Image object (do sprawdzenia Ctrl w Update)
                    currentImageObject = hitObj;

                    // nie pokazuj hoverUI gdy caption jest null/empty lub "[no caption]"
                    if (string.IsNullOrEmpty(caption) || caption.Trim().Equals("[no caption]", StringComparison.OrdinalIgnoreCase))
                    {
                        // włącz outline i dźwięk (opcjonalnie)
                        if (!hoverSoundPlayed)
                        {
                            EnsureAndEnableOutline(hitObj);
                            PlaySound();
                            hoverSoundPlayed = true;
                        }
                        SetCrosshair(highlightedCrosshair);
                        HideHoverUI();
                        currentImageObject = null; // wyczyść gdy brak caption
                        return;
                    }

                    if (!hoverSoundPlayed)
                    {
                        EnsureAndEnableOutline(hitObj);
                        PlaySound();
                        hoverSoundPlayed = true;
                    }

                    SetCrosshair(highlightedCrosshair);
                    ShowHoverUI(caption);
                    return;
                }

                // graj dźwięk tylko gdy to nie jest Portal (pozostałe tagi)
                if (!hoverSoundPlayed && hitTag != "PortalBack" && hitTag != "PortalNext")
                {
                    // włącz outline na bieżącym obiekcie
                    EnsureAndEnableOutline(hitObj);
                    PlaySound();
                    ShowHoverUI(hitObj.name);
                    hoverSoundPlayed = true;
                }

                SetCrosshair(highlightedCrosshair);
                ShowHoverUI(null);
                return;
            }
        }

        // nic nie trafione albo poza zasięgiem/nie pasujący tag
        DisableLastOutline();
        lastHoveredObject = null;
        hoverSoundPlayed = false;
        currentImageObject = null; // wyczyść Image object gdy nic nie jest najechane
        // isHoverUIInteractive = false; // wyłącz tryb interaktywny
        SetCrosshair(normalCrosshair);
        HideHoverUI();
    }

    void SetCrosshair(Sprite sprite)
    {
        if (crosshairImage == null) return;
        if (crosshairImage.sprite != sprite)
        {
            crosshairImage.sprite = sprite;
        }
    }

    void PlaySound()
    {
        if (hoverSound == null || audioSource == null) return;
        audioSource.PlayOneShot(hoverSound, hoverVolume);
    }

    // Znajdź lub dodaj Outline, ustaw parametry i włącz
    void EnsureAndEnableOutline(GameObject obj)
    {
        // Nie dodawaj outline do portali
        if (obj.CompareTag("PortalBack") || obj.CompareTag("PortalNext"))
            return;
        
        // spróbuj znaleźć na obiekcie lub jego rodzicu (np. gdy collider jest na childzie)
        Outline outline = obj.GetComponent<Outline>();
        if (outline == null && obj.transform.parent != null)
            outline = obj.transform.parent.GetComponent<Outline>();

        if (outline == null && addOutlineIfMissing)
        {
            outline = obj.AddComponent<Outline>();
        }

        if (outline != null)
        {
            // zapamiętaj jako aktywny outline
            lastOutline = outline;

            // ustawienia (API Outline może różnić się zależnie od wersji)
            outline.enabled = true;
            setOutlineColor(outline, outlineColor);
            setOutlineWidth(outline, outlineWidth);
        }
    }

    void DisableLastOutline()
    {
        if (lastOutline != null)
        {
            lastOutline.enabled = false;
            lastOutline = null;
        }
    }

    // Pomocnicze metody do ustawiania właściwości w różnych wersjach Outline
    void setOutlineColor(Outline outline, Color color)
    {
        try { outline.OutlineColor = color; } catch {}
    }

    void setOutlineWidth(Outline outline, float width)
    {
        try { outline.OutlineWidth = width; } catch {}
    }

    // ShowHoverUI przyjmuje opcjonalną wiadomość (np. bookName)
    void ShowHoverUI(string message = null)
    {
        if (hoverUI == null) return;

        // ustaw tekst jeśli podano (jeśli podano pusty string — wyczyści)
        if (hoverText != null)
            hoverText.Source = message ?? string.Empty;

        if (!hoverUI.activeSelf) hoverUI.SetActive(true);
    }

    void HideHoverUI()
    {
        if (hoverUI == null) return;
        if (hoverText != null) hoverText.Source = string.Empty;
        if (hoverUI.activeSelf) hoverUI.SetActive(false);
    }

    // Zwraca IInteractable obecnie najechanego obiektu (null jeśli brak)
    public IInteractable GetCurrentInteractable()
    {
        if (lastHoveredObject == null) return null;
        var interactable = lastHoveredObject.GetComponent<IInteractable>();
        if (interactable == null && lastHoveredObject.transform.parent != null)
            interactable = lastHoveredObject.transform.parent.GetComponent<IInteractable>();
        return interactable;
    }

    // Zwraca czy hover UI jest w trybie interaktywnym (Ctrl wciśnięty dla Image)
    public bool IsHoverUIInteractive()
    {
        return isHoverUIInteractive;
    }
}
