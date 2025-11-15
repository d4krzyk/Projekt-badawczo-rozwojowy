using UnityEngine;
using UnityEngine.UI;

public class CursorController : MonoBehaviour
{
    [Header("Crosshair Settings")]
    public Image crosshairImage;
    public Sprite normalCrosshair;
    public Sprite highlightedCrosshair;

    [Header("Raycast Settings")]
    public Camera mainCamera;
    // fallbackowa wartość — jeżeli nie znajdzie Controllera użyje tego
    public float rayDistance = 1f;

    [Header("Audio")]
    public AudioClip hoverSound;
    [Range(0f,1f)] public float hoverVolume = 0.5f;
    public AudioSource audioSource;

    // opcjonalne powiązanie z Twoim Controllerem - jeśli jest przypisane, używamy jego interactDistance
    public Controller playerController;

    // pole śledzące ostatnio najechany obiekt
    GameObject lastHoveredObject;

    // flaga informująca, czy dla aktualnie najechanego obiektu już zagrano dźwięk
    bool hoverSoundPlayed = false;

    void Start()
    {
        if (crosshairImage == null)
            Debug.LogError("Crosshair Image not assigned!");
        if (mainCamera == null)
            mainCamera = Camera.main;

        // spróbuj automatycznie znaleźć Controller, jeśli nie przypisano w inspektorze
        if (playerController == null)
        {
            playerController = FindObjectOfType<Controller>();
        }

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

    }

    void LateUpdate()
    {
        UpdateCrosshair();
    }

    void UpdateCrosshair()
    {
        if (mainCamera == null)
            return;

        // efektywna odległość interakcji — pobierana z Controller.interactDistance jeśli dostępna
        float effectiveDistance = (playerController != null) ? playerController.interactDistance : rayDistance;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // przesunięcie originu na near clip plane, żeby nie trafiać we własne kolidery kamery
        float startOffset = mainCamera.nearClipPlane + 0.005f;
        Vector3 origin = ray.origin + ray.direction * startOffset;
        ray = new Ray(origin, ray.direction);

        // długość castu musi uwzględniać startOffset bo Raycast liczy od origin
        float castDistance = effectiveDistance + startOffset;

        Debug.DrawRay(ray.origin, ray.direction * castDistance, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, castDistance))
        {
            Debug.DrawLine(ray.origin, hit.point, Color.green);

            // oblicz prawdziwy dystans od kamery do punktu trafienia
            float distanceFromCamera = Vector3.Distance(mainCamera.transform.position, hit.point);

            if (hit.collider.CompareTag("Book") && distanceFromCamera <= effectiveDistance)
            {
                GameObject hitObj = hit.collider.gameObject;

                // jeśli najechano na inną książkę niż wcześniej -> zresetuj flagę odtwarzania
                if (hitObj != lastHoveredObject)
                {
                    lastHoveredObject = hitObj;
                    hoverSoundPlayed = false;
                }

                // jeśli jeszcze nie zagrano dźwięku dla tej książki -> odtwórz i ustaw flagę
                if (!hoverSoundPlayed)
                {
                    PlaySound();
                    hoverSoundPlayed = true;
                }

                SetCrosshair(highlightedCrosshair);
                return;
            }
        }

        // gdy nic nie trafione albo poza zasięgiem/nie książka - resetujemy stan
        lastHoveredObject = null;
        hoverSoundPlayed = false;
        SetCrosshair(normalCrosshair);
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
}
