using System;
using System.Collections.Generic;
using LogicUI.FancyTextRendering;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 3.2f;
    public float gravity = 9.81f;
    public float mouseSensitivity = 2f;
    public float interactDistance = 2f;
    public Transform cameraTransform;


    public InfoboxGenerator infoboxGenerator; // Dodaj referencję do InfoboxGenerator
    public MarkdownRenderer leftPage;
    public MarkdownRenderer rightPage;
    public BookController bookController;
    public Logger logger;
    // sprint: dodatkowa prędkość gdy trzymamy Shift + ruch
    public float sprintBonus = 2f;

    [Header("Jump")]
    public float jumpVelocity = 4.5f;
    public float jumpCooldown = 0.1f;

    [Header("Crouch")]
    [Range(0.3f, 1f)] public float crouchScaleY = 0.6f;
    [Range(0.2f, 1f)] public float crouchSpeedMultiplier = 0.7f;
    [Min(0.1f)] public float crouchTransitionSpeed = 10f;
    
    [Header("Camera Zoom")]
    public float defaultFOV = 60f;
    public float zoomFOV = 5f;
    public float zoomSpeed = 5f;

    // nowe pole do blokowania ruchu z zewnątrz (np. PortalLift)
    [HideInInspector] public bool movementLocked = false;

    // --- proste parametry bob + footsteps ---
    [Header("Camera Bob / Footsteps")]
    public float bobAmount = 0.05f;            // ile kamera schodzi (metry)
    public float baseStepSpeed = 1.7f;         // tempo bobbingu przy domyślnym moveSpeed
    public AudioSource footstepSource;
    public AudioClip footstepClip;
    [Range(0f, 1f)] public float footstepVolume = 0.35f;
    public float groundCheckDistance = 1.1f;   // odległość sprawdzająca czy jesteśmy na ziemi

    // --- dźwięki otwierania/zamykania książki ---
    [Header("Book Sounds")]
    public AudioClip openBookClip;
    public AudioClip closeBookClip;
    [Range(0f, 1f)] public float bookSoundVolume = 0.5f;
    public AudioSource bookAudioSource;


    float xRotation = 0f;
    bool isReading = false;
    bool isPaused = false;
    BookInteraction currentBook;
    Vector2Int hexPosition;
    float openBookTime;

    // stan bob/step
    float defaultCamY;
    float bobTimer = 0f;
    bool playedThisStep = false;
    // character controller + vertical velocity (przeniesione z FPSCharacterWalkController)
    Rigidbody rb;
    // yaw dla stabilnej rotacji postaci
    float yaw = 0f;
    bool jumpQueued = false;
    float lastJumpTime = -999f;
    Vector3 standingScale;
    bool crouchHeld = false;

    [Header("UI")]
    public GameObject BookUI;
    public GameObject ImageUI;
    public GameObject InfoBoxUI;
    public GameObject SecondaryInfoBoxUI;
    public GameObject PauseUI;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        standingScale = transform.localScale;
        yaw = transform.eulerAngles.y;
        LockCursor();

        hexPosition = DataCollectionUtil.PixelToHexPos(new Vector2(transform.position.x, transform.position.z));
        System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ".";
        System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

        // inicjalizacja bob
        if (cameraTransform != null) defaultCamY = cameraTransform.localPosition.y;

        // przygotuj AudioSource dla dźwięków książki, jeśli nie przypisano w inspectorze
        if (bookAudioSource == null)
        {
            bookAudioSource = gameObject.AddComponent<AudioSource>();
            bookAudioSource.playOnAwake = false;
            bookAudioSource.spatialBlend = 0f;
        }
    }

    void Update()
    {
        HandlePause();
        HandleMouseLook();
        HandleCrouch();
        HandleJumpInput();
        HandleInteraction();
        HandleCameraZoom();
        HandleCameraBobAndFootsteps();

        Vector2Int newPosition = DataCollectionUtil.PixelToHexPos(new Vector2(transform.position.x, transform.position.z));
        if (hexPosition != newPosition)
        {
            hexPosition = newPosition;
            // Budujemy string bez interpolacji z klamrami, liczby formatujemy do 2 miejsc po przecinku
            string currentMove =
                hexPosition.x + " " + hexPosition.y + " " +
                transform.position.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " " +
                transform.position.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " " +
                Time.time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

            List<float> currentMoveFloat = new List<float> { hexPosition.x, hexPosition.y, transform.position.x, transform.position.z, Time.time };
            if (logger != null) logger.UpdateCurrentPath(currentMoveFloat);
        }
    }

    void FixedUpdate()
    {
        HandleMovement();
        HandleJump();
    }

    void HandleJumpInput()
    {
        if (isPaused || isReading || movementLocked) return;
        if (IsCrouching()) return;

        if (Input.GetKeyDown(KeyCode.Space))
            jumpQueued = true;
    }

    void HandleCrouch()
    {
        bool canCrouch = !isPaused && !isReading && !movementLocked;
        crouchHeld = canCrouch && Input.GetKey(KeyCode.C);

        float targetY = crouchHeld ? standingScale.y * crouchScaleY : standingScale.y;
        Vector3 currentScale = transform.localScale;
        currentScale.y = Mathf.MoveTowards(currentScale.y, targetY, crouchTransitionSpeed * Time.deltaTime);
        transform.localScale = currentScale;
    }

    void HandleMovement()
    {
        // jeśli gra jest wstrzymana, nie poruszaj się
        if (isPaused) return;

        // zablokuj ruch gdy czytamy lub movementLocked ustawione z zewnątrz (np. PortalLift)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // wykryj trzymanie Shift oraz czy jest input ruchu
        bool sprintKey = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool hasMovementInput = Mathf.Abs(horizontal) > 0f || Mathf.Abs(vertical) > 0f;

        // zastosuj bonus tylko gdy gracz rzeczywiście idzie i nie czyta/nie jest zablokowany
        float currentSpeed = moveSpeed;
        if (sprintKey && hasMovementInput && !isReading && !movementLocked)
            currentSpeed += sprintBonus;
        if (IsCrouching())
            currentSpeed *= crouchSpeedMultiplier;

        Vector3 rawMove = transform.right * horizontal + transform.forward * vertical;
        Vector3 move = (isReading || movementLocked) ? Vector3.zero : (rawMove.sqrMagnitude > 1f ? rawMove.normalized : rawMove);

        if (rb != null && !rb.isKinematic)
        {
            Vector3 currentVelocity = rb.linearVelocity;
            Vector3 targetHorizontalVelocity = move * currentSpeed;
            rb.linearVelocity = new Vector3(targetHorizontalVelocity.x, currentVelocity.y, targetHorizontalVelocity.z);
        }
        else
        {
            Vector3 delta = move * currentSpeed * Time.fixedDeltaTime;
            transform.position += delta;
        }
    }

    void HandleJump()
    {
        if (!jumpQueued) return;

        jumpQueued = false;

        if (rb == null || rb.isKinematic) return;
        if (isPaused || isReading || movementLocked) return;
        if (IsCrouching()) return;
        if (Time.time - lastJumpTime < jumpCooldown) return;
        if (!IsGrounded()) return;

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y < 0f)
            velocity.y = 0f;
        velocity.y = jumpVelocity;
        rb.linearVelocity = velocity;
        lastJumpTime = Time.time;
    }

    bool IsCrouching()
    {
        return transform.localScale.y < standingScale.y - 0.01f;
    }

    void HandleMouseLook()
    {
        if (isPaused) return;

        bool canLook = !isReading && !movementLocked;
        if (!canLook) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    // Wymusza określony yaw/pitch (używane przy teleportacji)
    public void ForceLook(float newYawDeg, float newPitchDeg)
    {
        yaw = newYawDeg;
        xRotation = Mathf.Clamp(newPitchDeg, -90f, 90f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraTransform)
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void HandlePause()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                TogglePause();
                return;
            }

            if (CloseReadingView())
                return;

            TogglePause();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        
        if (PauseUI != null)
            PauseUI.SetActive(isPaused);

        if (isPaused)
            UnlockCursor();
        else
        {
            LockCursor();

        }
    }

    public bool IsPaused()
    {
        return isPaused;
    }

    public void PlayUISound(AudioClip clip, float volume = 1f)
    {
        if (bookAudioSource != null && clip != null)
            bookAudioSource.PlayOneShot(clip, volume);
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void HandleInteraction()
    {
        // zablokuj interakcje gdy movementLocked (np. w trakcie liftu)
        if (movementLocked) return;

        // zablokuj interakcje gdy gra jest wstrzymana
        if (isPaused) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isReading)
            {
                CloseReadingView();
                return;
            }
            if (cameraTransform == null) return;
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (hit.collider.CompareTag("Book") && interactable != null)
                {
                    // jeśli to książka - odtwórz dźwięk otwarcia i przejdź do trybu czytania
                    var book = interactable as BookInteraction;
                    if (book != null)
                    {
                        if (bookAudioSource != null && openBookClip != null)
                            bookAudioSource.PlayOneShot(openBookClip, bookSoundVolume);

                        interactable.OnInteraction();
                        currentBook = book;
                        
                        // Sanityzuj zawartość książki przed renderowaniem
                        string sanitizedContent = SanitizeMarkdownContent(currentBook.content);
                        leftPage.Source = sanitizedContent;
                        rightPage.Source = sanitizedContent;
                        
                        if (BookUI != null) BookUI.SetActive(true);
                        if (InfoBoxUI != null) InfoBoxUI.SetActive(false);
                        bookController.ResetPages();
                        isReading = true;
                        UnlockCursor();
                        openBookTime = Time.time;
                        return;
                    }
                    interactable.OnInteraction();
                }
                else if(hit.collider.CompareTag("InfoBox"))
                {
                    // obsługa InfoBox: wykryj po tagu "InfoBox" lub komponencie InfoBoxInteraction
                    var infoComp = hit.collider.GetComponent<InfoBoxInteraction>();
                    if (infoComp != null || hit.collider.CompareTag("InfoBox"))
                    {
                        if (!CanOpenInfoBox())
                            return;

                        if (bookAudioSource != null && openBookClip != null)
                            bookAudioSource.PlayOneShot(openBookClip, bookSoundVolume);

                        // Resetuj pozycję contentu przed otwarciem
                        if (infoboxGenerator != null && infoboxGenerator.contentTransform != null)
                        {
                            Vector2 anchoredPos = infoboxGenerator.contentTransform.anchoredPosition;
                            anchoredPos.y = 180f;
                            infoboxGenerator.contentTransform.anchoredPosition = anchoredPos;
                        }

                        if (InfoBoxUI != null) 
                        {
                            if (infoComp.content == "1")
                            {
                                InfoBoxUI.SetActive(true);
                            }
                            else
                            {
                                SecondaryInfoBoxUI.SetActive(true);
                            }
                        }
                        if (BookUI != null) BookUI.SetActive(false);
                        currentBook = null;
                        isReading = true;
                        UnlockCursor();
                        openBookTime = Time.time;
                        return;
                    }
                }
                else if(hit.collider.CompareTag("Image"))
                {
                    IInteractable imgInteractable = hit.collider.GetComponent<IInteractable>();
                    Debug.Log($"Wykryto interakcję z obrazem: {hit.collider.name}");
                    

                    // Najpierw wywołaj OnInteraction, aby ustawić texture i caption
                    imgInteractable?.OnInteraction();
                    
                    // Potem aktywuj UI
                    if (ImageUI != null) 
                    {
                        ImageUI.SetActive(true);
                        Debug.Log("ImageUI aktywowane");
                    }
                    else
                    {
                        Debug.LogError("ImageUI jest null!");
                    }
                    
                    if (InfoBoxUI != null) InfoBoxUI.SetActive(false);
                    if (BookUI != null) BookUI.SetActive(false);
                    UnlockCursor();
                    currentBook = null;
                    isReading = true;
                    return;
                }
            }

        }

    }

    public bool CloseReadingView(bool playCloseSound = true)
    {
        bool hasOpenImage = ImageUI != null && ImageUI.activeSelf;
        bool hasOpenBook = BookUI != null && BookUI.activeSelf;
        bool hasOpenInfoBox = InfoBoxUI != null && InfoBoxUI.activeSelf;
        bool hasOpenSecondaryInfoBox = SecondaryInfoBoxUI != null && SecondaryInfoBoxUI.activeSelf;

        if (!isReading && !hasOpenImage && !hasOpenBook && !hasOpenInfoBox && !hasOpenSecondaryInfoBox)
            return false;

        if (playCloseSound && bookAudioSource != null && closeBookClip != null)
            bookAudioSource.PlayOneShot(closeBookClip, bookSoundVolume);

        if (hasOpenImage)
        {
            ImageZoomPan zoomPan = ImageUI.GetComponentInChildren<ImageZoomPan>();
            if (zoomPan != null) zoomPan.ResetPosition();
            ImageUI.SetActive(false);
        }

        if (hasOpenBook)
            BookUI.SetActive(false);
        if (hasOpenInfoBox)
            InfoBoxUI.SetActive(false);
        if (hasOpenSecondaryInfoBox)
            SecondaryInfoBoxUI.SetActive(false);

        if (currentBook != null)
        {
            currentBook.OnInteraction();
            if (logger != null)
                logger.LogOnBookClose(currentBook.bookArticleLink, openBookTime, Time.time);
            currentBook = null;
        }

        isReading = false;
        LockCursor();
        return true;
    }

    void HandleCameraZoom()
    {
        if (cameraTransform == null) return;
        
        Camera cam = cameraTransform.GetComponent<Camera>();
        if (cam == null) return;
        
        bool zoomKey = Input.GetKey(KeyCode.Z);
        float targetFOV = zoomKey ? zoomFOV : defaultFOV;
        
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
    }

    // proste przeniesione zachowanie: jeśli gracz się porusza i stoi na ziemi ->
    // sinusoidalny bob (im szybciej moveSpeed tym szybciej) oraz pojedynczy dźwięk raz na "dół" fali
    void HandleCameraBobAndFootsteps()
    {
        if (cameraTransform == null) return;

        // czy gracz podaje input ruchu?
        bool inputMove = (Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f);
        bool grounded = IsGrounded();

        // wykryj sprint (Shift) i czy jest rzeczywisty input ruchu
        bool sprintKey = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool sprinting = sprintKey && inputMove && !isReading && !movementLocked;

        bool movingAndGrounded = inputMove && grounded && !isReading && !movementLocked;

        if (movingAndGrounded)
        {
            // tempo bob zależne od moveSpeed oraz dodatkowo zwiększone podczas sprintu
            float speedForBob = moveSpeed + (sprinting ? sprintBonus : 0f);
            float bobSpeed = baseStepSpeed * (speedForBob / Mathf.Max(0.1f, moveSpeed));
            bobTimer += Time.deltaTime * bobSpeed;

            float yOffset = Mathf.Sin(bobTimer * Mathf.PI) * bobAmount;
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                defaultCamY + Mathf.Abs(yOffset),
                cameraTransform.localPosition.z
            );

            // odtwórz dźwięk dwa razy na cykl (góra i dół fali) by zwiększyć częstotliwość kroków
            float s = Mathf.Sin(bobTimer * Mathf.PI);
            if (Mathf.Abs(s) > 0.9f)
            {
                if (!playedThisStep)
                {
                    PlayStep(sprinting);
                    playedThisStep = true;
                }
            }
            else
            {
                playedThisStep = false;
            }
        }
        else
        {
            // resetuj i płynnie ustaw kamerę z powrotem
            bobTimer = 0f;
            playedThisStep = false;
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                Mathf.Lerp(cameraTransform.localPosition.y, defaultCamY, Time.deltaTime * 8f),
                cameraTransform.localPosition.z
            );
        }
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance);
    }

    void PlayStep(bool isSprinting)
    {
        if (footstepSource == null || footstepClip == null) return;
        // lekko podbij pitch podczas sprintu, aby brzmiało szybciej
        float basePitch = UnityEngine.Random.Range(0.95f, 1.05f);
        footstepSource.pitch = basePitch * (isSprinting ? 1.1f : 1f);
        footstepSource.PlayOneShot(footstepClip, footstepVolume);
    }

    // Sanityzuje zawartość markdown, usuwając nieprawidłowe formatowanie linków
    string SanitizeMarkdownContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        
        // Usuń puste lub nieprawidłowe linki markdown []() lub [text]()
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\[([^\]]*)\]\(\s*\)", "$1");
        
        // Usuń linki bez tekstu [](url)
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\[\]\([^\)]*\)", "");
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\[([^\]]+)\](?!\()", "$1");
        return content;
    }

    bool CanOpenInfoBox()
    {
        RoomsController roomsController = FindAnyObjectByType<RoomsController>();
        if (roomsController == null || roomsController.elongatedRoom == null)
            return true;

        InfoboxGenerator infoboxStatus = roomsController.elongatedRoom.infoboxGenerator;
        return infoboxStatus == null || !infoboxStatus.HasFailed;
    }

}
