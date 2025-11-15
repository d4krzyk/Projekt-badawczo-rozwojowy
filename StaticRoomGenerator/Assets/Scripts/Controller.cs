using System;
using LogicUI.FancyTextRendering;
using UnityEngine;

[RequireComponent(typeof(CharacterController))
]
public class Controller : MonoBehaviour
{
    public float moveSpeed = 3.2f;
    public float gravity = 9.81f;
    public float mouseSensitivity = 2f;
    public float interactDistance = 2f;
    public Transform cameraTransform;
    public GameObject BookUI;
    public MarkdownRenderer leftPage;
    public MarkdownRenderer rightPage;
    public BookController bookController;
    public Logger logger;

    // --- proste parametry bob + footsteps ---
    [Header("Camera Bob / Footsteps")]
    public float bobAmount = 0.05f;            // ile kamera schodzi (metry)
    public float baseStepSpeed = 1.7f;         // tempo bobbingu przy domyślnym moveSpeed
    public AudioSource footstepSource;
    public AudioClip footstepClip;
    [Range(0f,1f)] public float footstepVolume = 0.35f;
    public float groundCheckDistance = 1.1f;   // odległość sprawdzająca czy jesteśmy na ziemi

    // --- dźwięki otwierania/zamykania książki ---
    [Header("Book Sounds")]
    public AudioClip openBookClip;
    public AudioClip closeBookClip;
    [Range(0f,1f)] public float bookSoundVolume = 0.5f;
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
    bool wasMoving = false;
    // character controller + vertical velocity (przeniesione z FPSCharacterWalkController)
    private CharacterController controller;
    private float verticalVelocity = 0f;

    void Start()
    {
        // wymagamy CharacterControllera i pobieramy go
        if (GetComponent<CharacterController>() == null) gameObject.AddComponent<CharacterController>();
        controller = GetComponent<CharacterController>();

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
        HandleMovement();
        HandleMouseLook();
        HandleInteraction();

        // obsługa prostego bob i kroków
        HandleCameraBobAndFootsteps();

        Vector2Int newPosition = DataCollectionUtil.PixelToHexPos(new Vector2(transform.position.x, transform.position.z));
        if (hexPosition != newPosition)
        {
            hexPosition = newPosition;
            string currentMove = $"{hexPosition.x} {hexPosition.y} {transform.position.x} {transform.position.z} {String.Format("{0:.##}", Time.time)} ";
            logger.UpdateCurrentPath(currentMove);
        }
    }

    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // input ruchu w lokalnych osiach
        Vector3 input = new Vector3(horizontal, 0f, vertical);
        Vector3 desiredMove = Vector3.zero;
        if (!isReading)
        {
            desiredMove = (transform.right * horizontal + transform.forward * vertical);
            if (desiredMove.sqrMagnitude > 1f) desiredMove.Normalize();
            desiredMove *= moveSpeed;
        }

        // gravity + CharacterController.Move (jak we FPSCharacterWalkController)
        if (controller.isGrounded)
            verticalVelocity = -0.5f;
        else
            verticalVelocity -= gravity * Time.deltaTime;

        desiredMove.y = verticalVelocity;
        controller.Move(desiredMove * Time.deltaTime);
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate the character left/right
        if(!isReading)
            transform.Rotate(Vector3.up * mouseX);

        // Rotate the camera up/down
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Prevent flipping

        if(!isReading)
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void HandleInteraction()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isReading)
            {
                // odtwórz dźwięk zamknięcia książki
                if (bookAudioSource != null && closeBookClip != null)
                    bookAudioSource.PlayOneShot(closeBookClip, bookSoundVolume);

                isReading = false;
                BookUI.SetActive(false);
                currentBook.OnInteraction();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                logger.LogOnBookClose(currentBook.bookArticleLink, openBookTime, Time.time);
                return;
            }

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    // jeśli to książka - odtwórz dźwięk otwarcia i przejdź do trybu czytania
                    if (interactable.GetType() == typeof(BookInteraction))
                    {
                        if (bookAudioSource != null && openBookClip != null)
                            bookAudioSource.PlayOneShot(openBookClip, bookSoundVolume);

                        interactable.OnInteraction();
                        currentBook = (BookInteraction)interactable;
                        leftPage.Source = currentBook.content;
                        rightPage.Source = currentBook.content;
                        BookUI.SetActive(true);
                        bookController.ResetPages();
                        isReading = true;
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                        openBookTime = Time.time;
                        return;
                    }

                    // ogólne interakcje nie-książkowe
                    interactable.OnInteraction();
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
        // CharacterController.isGrounded (stabilniejsze)
        bool grounded = controller != null ? controller.isGrounded : Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance);
        bool movingAndGrounded = inputMove && grounded && !isReading;

        if (movingAndGrounded)
        {
            // tempo bob zależne od moveSpeed
            float bobSpeed = baseStepSpeed * (moveSpeed / 2f);
            bobTimer += Time.deltaTime * bobSpeed;

            float yOffset = Mathf.Sin(bobTimer * Mathf.PI) * bobAmount;
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                defaultCamY + Mathf.Abs(yOffset),
                cameraTransform.localPosition.z
            );

            // odtwórz dźwięk raz gdy fala osiąga "dół"
            if (Mathf.Sin(bobTimer * Mathf.PI) < -0.9f)
            {
                if (!playedThisStep)
                {
                    PlayStep();
                    playedThisStep = true;
                }
            }
            else
            {
                playedThisStep = false;
            }

            wasMoving = true;
        }
        else
        {
            // resetuj i płynnie ustaw kamerę z powrotem
            bobTimer = 0f;
            playedThisStep = false;
            wasMoving = false;
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                Mathf.Lerp(cameraTransform.localPosition.y, defaultCamY, Time.deltaTime * 8f),
                cameraTransform.localPosition.z
            );
        }
    }

    void PlayStep()
    {
        if (footstepSource == null || footstepClip == null) return;
        footstepSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
        footstepSource.PlayOneShot(footstepClip, footstepVolume);
    }
}
