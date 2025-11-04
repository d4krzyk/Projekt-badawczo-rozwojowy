using UnityEngine;

public class RoomsController : MonoBehaviour
{
    public RoomGeneration currentRoom;
    public RoomGeneration secondRoom;

    void Start()
    {
        currentRoom.GenerateRoom(currentRoom.articleName);
        currentRoom.previousRoom = "";
    }

    public void SwapRooms()
    {
        RoomGeneration temp = currentRoom;
        currentRoom = secondRoom;
        secondRoom = temp;


        currentRoom.enterTime = Time.time;
        secondRoom.exitTime = Time.time;
        currentRoom.previousRoom = secondRoom.articleData.name;
        secondRoom.LogRoom();
    }


}
