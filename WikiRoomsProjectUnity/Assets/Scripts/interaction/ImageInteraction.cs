using UnityEngine;
using UnityEngine.UI;

public class ImageInteraction : MonoBehaviour, IInteractable
{
    [Header("Image Data")]
    public string caption;
    public Texture texture;

    public void OnInteraction()
    {
        // Pobierz ImageUI z PlayerController
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("Nie znaleziono PlayerController!");
            return;
        }

        GameObject imageUI = player.ImageUI;
        if (imageUI == null)
        {
            Debug.LogError("ImageUI nie jest przypisane w PlayerController!");
            return;
        }

        Debug.Log($"ImageInteraction: Ustawiam obraz. Caption: {caption}, Texture: {texture?.name}");

        // Znajdź RawImage w dzieciach imageUI
        RawImage rawImage = imageUI.GetComponentInChildren<RawImage>(true);
        if (rawImage != null)
        {
            rawImage.texture = texture;
            rawImage.SetNativeSize();
            Debug.Log($"Ustawiono texture na RawImage: {rawImage.name}");
        }
        else
        {
            Debug.LogError("Nie znaleziono RawImage w ImageUI!");
        }

        // Znajdź TextMeshProUGUI dla caption
        // TMPro.TextMeshProUGUI captionText = imageUI.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        // if (captionText != null)
        // {
        //     captionText.text = caption;
        //     Debug.Log($"Ustawiono caption: {caption}");
        // }
        // else
        // {
        //     Debug.LogWarning("Nie znaleziono TextMeshProUGUI dla caption w ImageUI!");
        // }
    }
}