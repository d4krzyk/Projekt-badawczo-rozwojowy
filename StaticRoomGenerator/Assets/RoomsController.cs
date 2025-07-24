using UnityEngine;

public class RoomsController : MonoBehaviour
{
    public RoomGeneration currentRoom;
    public RoomGeneration secondRoom;

    void Start()
    {
        currentRoom.GenerateRoom(currentRoom.articleName);
    }

    public void SwapRooms()
    {
        RoomGeneration temp = currentRoom;
        currentRoom = secondRoom;
        secondRoom = temp;
    }


}
