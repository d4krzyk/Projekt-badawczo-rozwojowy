using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Tooltip("Opcjonalny lokalny clip hover (używany jeśli nie ma MenuController.hoverSound)")]
    public AudioClip localHoverClip;
    [Tooltip("Opcjonalny lokalny clip click (używany jeśli nie ma MenuController.clickSound)")]
    public AudioClip localClickClip;
    [Range(0f,1f)] public float volume = 1f;

    AudioSource src;
    MenuController menuCtrl;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;

        menuCtrl = FindFirstObjectByType<MenuController>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (menuCtrl != null && menuCtrl.hoverSound != null && menuCtrl.audioSource != null)
        {
            menuCtrl.PlayHoverSound();
            return;
        }

        if (localHoverClip != null)
            src.PlayOneShot(localHoverClip, volume);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (menuCtrl != null && menuCtrl.clickSound != null && menuCtrl.audioSource != null)
        {
            menuCtrl.PlayClickSound();
            return;
        }

        if (localClickClip != null)
            src.PlayOneShot(localClickClip, volume);
    }
}