using UnityEngine;

public class TeleportController : MonoBehaviour
{
    public Transform player;
    public Transform teleportPoint;
    public RoomsController gameController;

    void TeleportPlayer()
    {
        player.position = teleportPoint.position;
    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider.GetComponent<Transform>() == player)
        {
            TeleportPlayer();
            gameController.SwapRooms();
        }
    }
}
