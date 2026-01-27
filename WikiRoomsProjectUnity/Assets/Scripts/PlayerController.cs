using System;
using LogicUI.FancyTextRendering;
using UnityEditor.Callbacks;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 3.2f;
    public float gravity = 9.81f;
    public float mouseSensitivity = 2f;
    public float interactDistance = 2f;
    public Transform cameraTransform;
    public GameObject BookUI;
    
    public GameObject InfoBoxUI;
    public MarkdownRenderer leftPage;
    public MarkdownRenderer rightPage;
    public BookController bookController;
    public Logger logger;
    // sprint: dodatkowa prędkość gdy trzymamy Shift + ruch
    public float sprintBonus = 2f;

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

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        yaw = transform.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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
            bookAudioSource.spatialBlend = 0f; // 2D sound (możesz ustawić 3D jeśli chcesz)
        }
    }

    void Update()
    {
        HandleMouseLook();
        HandleInteraction();
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
            if (logger != null) logger.UpdateCurrentPath(currentMove);
        }
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
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

        Vector3 rawMove = transform.right * horizontal + transform.forward * vertical;
        Vector3 move = (isReading || movementLocked) ? Vector3.zero : (rawMove.sqrMagnitude > 1f ? rawMove.normalized : rawMove);
        transform.position += move * currentSpeed * Time.fixedDeltaTime;
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Akumuluj yaw i ustaw rotację postaci w kontrolowany sposób (zapobiega dryfowi)
        if (!isReading && !movementLocked)
        {
            yaw += mouseX;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // Rotate the camera up/down
        if (!isReading && !movementLocked)
        {
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Prevent flipping
        }

        if (cameraTransform != null)
        {
            // kamera kontroluje pitch niezależnie od rotacji postaci
            if (!isReading && !movementLocked) cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            else
            {
                // gdy czytamy/lock zwolniony - nadal zachowaj pitch ustawiony przez gracza
                cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            }
        }

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

    void HandleInteraction()
    {
        // zablokuj interakcje gdy movementLocked (np. w trakcie liftu)
        if (movementLocked) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isReading)
            {
                // odtwórz dźwięk zamknięcia książki / infoboxa
                if (bookAudioSource != null && closeBookClip != null)
                    bookAudioSource.PlayOneShot(closeBookClip, bookSoundVolume);

                isReading = false;
                if (BookUI != null) BookUI.SetActive(false);
                if (InfoBoxUI != null) InfoBoxUI.SetActive(false);
                currentBook?.OnInteraction();
                currentBook = null;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (currentBook != null && logger != null)
                    logger.LogOnBookClose(currentBook.bookArticleLink, openBookTime, Time.time);
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
                        leftPage.Source = currentBook.content;
                        rightPage.Source = currentBook.content;
                        if (BookUI != null) BookUI.SetActive(true);
                        if (InfoBoxUI != null) InfoBoxUI.SetActive(false);
                        bookController.ResetPages();
                        isReading = true;
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
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
                        if (bookAudioSource != null && openBookClip != null)
                            bookAudioSource.PlayOneShot(openBookClip, bookSoundVolume);

                        if (InfoBoxUI != null) InfoBoxUI.SetActive(true);
                        if (BookUI != null) BookUI.SetActive(false);
                        currentBook = null;
                        isReading = true;
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                        openBookTime = Time.time;
                        return;
                    }
                }
            }

        }

    }

    // proste przeniesione zachowanie: jeśli gracz się porusza i stoi na ziemi ->
    // sinusoidalny bob (im szybciej moveSpeed tym szybciej) oraz pojedynczy dźwięk raz na "dół" fali
    void HandleCameraBobAndFootsteps()
    {
        if (cameraTransform == null) return;

        // czy gracz podaje input ruchu?
        bool inputMove = (Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f);
        bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance);

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

    void PlayStep(bool isSprinting)
    {
        if (footstepSource == null || footstepClip == null) return;
        // lekko podbij pitch podczas sprintu, aby brzmiało szybciej
        float basePitch = UnityEngine.Random.Range(0.95f, 1.05f);
        footstepSource.pitch = basePitch * (isSprinting ? 1.1f : 1f);
        footstepSource.PlayOneShot(footstepClip, footstepVolume);
    }

}
