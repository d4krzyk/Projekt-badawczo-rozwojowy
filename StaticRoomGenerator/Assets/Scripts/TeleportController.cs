using UnityEngine;

public class TeleportController : MonoBehaviour
{
    public Transform player;
    public Transform teleportPoint;
    RoomsController _gameController;
    public RoomsController gameController
    {
        get
        {
            if (_gameController == null)
                _gameController = FindObjectOfType<RoomsController>();
            return _gameController;
        }
    }

    void TeleportPlayer()
    {
        player.position = teleportPoint.position;
    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider.CompareTag("Player"))
        {
            TeleportPlayer();
            gameController.SwapRooms();
        }
    }
}
