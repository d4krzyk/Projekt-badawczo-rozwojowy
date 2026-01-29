using UnityEngine;
using UnityEngine.EventSystems;

public class ImageZoomPan : MonoBehaviour, IDragHandler, IScrollHandler
{
    public RectTransform imageRect;
    public float zoomSpeed = 0.1f;
    public float minScale = 0.5f;
    public float maxScale = 3f;

    private Vector2 originalPosition;

    void Start()
    {
        if(imageRect == null) imageRect = GetComponent<RectTransform>();
        originalPosition = Vector2.zero;
    }

    // Dragowanie myszką
    public void OnDrag(PointerEventData eventData)
    {
        imageRect.anchoredPosition += eventData.delta;
    }

    // Zoom kółkiem myszy
    public void OnScroll(PointerEventData eventData)
    {
        float scale = imageRect.localScale.x;
        scale += eventData.scrollDelta.y * zoomSpeed;
        scale = Mathf.Clamp(scale, minScale, maxScale);
        imageRect.localScale = Vector3.one * scale;
    }

    // Reset pozycji i skali
    public void ResetPosition()
    {
        imageRect.anchoredPosition = Vector2.zero;
        imageRect.localScale = Vector3.one;
    }
}
