using UnityEngine;

public class RoomsController : MonoBehaviour
{
    public RoomGeneration currentRoom;
    public RoomGeneration secondRoom;
    public ElongatedRoomGenerator elongatedRoom;
    public ElongatedRoomGenerator secondElongatedRoom;

    void Start()
    {
        // currentRoom.GenerateRoom(currentRoom.articleName);
        elongatedRoom.GenerateRoom(elongatedRoom.articleName);
        currentRoom.previousRoom = "";
    }

    // public void SwapRooms()
    // {
    //     RoomGeneration temp = currentRoom;
    //     currentRoom = secondRoom;
    //     secondRoom = temp;


    //     currentRoom.EnterTime = Time.time;
    //     secondRoom.exitTime = Time.time;
    //     currentRoom.previousRoom = secondRoom.articleData.name;
    //     secondRoom.LogRoom();
    // }

    public void SwapRooms()
    {
        ElongatedRoomGenerator temp = elongatedRoom;
        elongatedRoom = secondElongatedRoom;
        secondElongatedRoom = temp;


        elongatedRoom.EnterTime = Time.time;
        secondElongatedRoom.ExitTime = Time.time;
        elongatedRoom.PreviousRoom = secondElongatedRoom.ArticleData.name;
        secondElongatedRoom.LogRoom();
    }

}
