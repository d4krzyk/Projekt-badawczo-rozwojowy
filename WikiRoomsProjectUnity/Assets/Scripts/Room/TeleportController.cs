using UnityEngine;

public class TeleportController : MonoBehaviour
{
    public Transform player;
    public Transform teleportPoint;
    public Transform mainCamera;
    public RoomsController gameController;

    void TeleportPlayer()
    {
        player.position = teleportPoint.position;
        player.rotation = Quaternion.Euler(0, 180, 0);

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController)
            playerController.ForceLook(180f, 0f);
        else if (mainCamera)
            mainCamera.localRotation = Quaternion.Euler(0, 0, 0);
    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider.GetComponent<Transform>() == player)
        {
            if (gameController.SwapRoomsPrevious()) TeleportPlayer();
        }
    }
}
