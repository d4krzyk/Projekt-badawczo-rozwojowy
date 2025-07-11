using LogicUI.FancyTextRendering;
using UnityEngine;

public class Controller : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;
    public float interactDistance = 3f;
    public Transform cameraTransform;
    public GameObject BookUI;
    public MarkdownRenderer leftPage;
    public MarkdownRenderer rightPage;
    public BookController bookController;

    float xRotation = 0f;
    bool isReading = false;
    BookInteraction currentBook;

    void Start()
    {
        // Lock cursor to the game window
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMovement();
        HandleMouseLook();
        HandleInteraction();
    }

    void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        transform.position += move * moveSpeed * Time.deltaTime;
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate the character left/right
        transform.Rotate(Vector3.up * mouseX);

        // Rotate the camera up/down
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Prevent flipping

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void HandleInteraction()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isReading)
            {
                isReading = false;
                BookUI.SetActive(false);
                currentBook.OnInteraction();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    interactable.OnInteraction();
                    if (interactable.GetType() == typeof(BookInteraction))
                    {
                        currentBook = (BookInteraction)interactable;
                        leftPage.Source = currentBook.content;
                        rightPage.Source = currentBook.content;
                        BookUI.SetActive(true);
                        bookController.ResetPages();
                        isReading = true;
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                }
            }

        }

    }
}
