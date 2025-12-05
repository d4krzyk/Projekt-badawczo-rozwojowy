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

            if (hit.collider.CompareTag("Book") && distanceFromCamera <= effectiveDistance)
            {
                GameObject hitObj = hit.collider.gameObject;

                if (hitObj != lastHoveredObject)
                {
                    // wyłącz outline z poprzedniego obiektu
                    DisableLastOutline();

                    lastHoveredObject = hitObj;
                    hoverSoundPlayed = false;
                }

                // włącz outline na bieżącym obiekcie
                EnsureAndEnableOutline(hitObj);

                if (!hoverSoundPlayed)
                {
                    PlaySound();
                    hoverSoundPlayed = true;
                }

                SetCrosshair(highlightedCrosshair);
                return;
            }
        }

        // nic nie trafione albo poza zasięgiem/nie książka
        DisableLastOutline();
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

    // Znajdź lub dodaj Outline, ustaw parametry i włącz
    void EnsureAndEnableOutline(GameObject obj)
    {
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
}
