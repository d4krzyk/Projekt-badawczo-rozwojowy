using UnityEngine;
using System.Collections;

public class BackTeleportController : MonoBehaviour
{
    public Transform player;
    public Transform teleportPoint;
    public Transform mainCamera;
    public RoomsController gameController;
    
    [Header("Audio")]
    public AudioClip suckBackClip;
    public float suckBackVolume = 1f;
    public float suckBackStartTime = 0f; // Od którego momentu clipu zacząć (w sekundach)
    
    [Header("Portal Pull")]
    public Transform portalBack;
    public float pullSpeed = 1.25f;
    public float pullDistance = 0.5f; // Odległość od portalu przy której następuje teleportacja
    
    [Header("Camera Effects")]
    public float maxFOVStretch = 120f; // Maksymalny FOV podczas rozciągania
    public AnimationCurve fovCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Krzywa FOV

    private bool isPulling = false;
    private float defaultFOV;

    void TeleportPlayer()
    {
        if (!isPulling && portalBack != null)
        {
            StartCoroutine(PullAndTeleport());
        }
    }

    IEnumerator PullAndTeleport()
    {
        isPulling = true;
        
        // Zablokuj ruch gracza
        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.movementLocked = true;
        }
        
        // Zapisz domyślny FOV
        Camera cam = mainCamera ? mainCamera.GetComponent<Camera>() : Camera.main;
        if (cam != null)
        {
            defaultFOV = cam.fieldOfView;
        }
        
        // Odtwórz dźwięk od określonego momentu
        AudioSource tempAudioSource = null;
        if (suckBackClip != null)
        {
            GameObject tempAudioObj = new GameObject("TempTeleportSound");
            tempAudioSource = tempAudioObj.AddComponent<AudioSource>();
            tempAudioSource.clip = suckBackClip;
            tempAudioSource.volume = suckBackVolume;
            tempAudioSource.spatialBlend = 0; // 0 = 2D, 1 = 3D
            tempAudioSource.time = suckBackStartTime; // Ustaw moment startu
            tempAudioSource.Play();
            
            // Usuń po zakończeniu
            float remainingTime = suckBackClip.length - suckBackStartTime;
            Destroy(tempAudioObj, remainingTime);
        }
        
        // Przybliżaj gracza do portalu z efektami kamery
        float startDistance = Vector3.Distance(player.position, portalBack.position);
        
        while (Vector3.Distance(player.position, portalBack.position) > pullDistance)
        {
            Vector3 direction = (portalBack.position - player.position).normalized;
            player.position += direction * pullSpeed * Time.deltaTime;
            
            // Rozciąganie FOV
            if (cam != null)
            {
                float currentDistance = Vector3.Distance(player.position, portalBack.position);
                float progress = 1f - (currentDistance / startDistance); // 0 na początku, 1 przy portalu
                float fovMultiplier = fovCurve.Evaluate(progress);
                cam.fieldOfView = Mathf.Lerp(defaultFOV, maxFOVStretch, fovMultiplier);
            }
            
            yield return null;
        }
        
        // Wykonaj teleportację
        player.position = teleportPoint.position;
        player.rotation = Quaternion.Euler(0, 180, 0);

        // Przywróć kamerę
        if (cam != null)
        {
            cam.fieldOfView = defaultFOV;
        }
        
        if (playerController)
            playerController.ForceLook(180f, 0f);
        else if (mainCamera)
            mainCamera.localRotation = Quaternion.Euler(0, 0, 0);
        
        // Odblokuj ruch
        if (playerController != null)
        {
            playerController.movementLocked = false;
        }
        
        isPulling = false;
    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider.GetComponent<Transform>() == player)
        {
            if (gameController.SwapRoomsPrevious()) TeleportPlayer();
        }
    }
}
