using System.Collections.Generic;
using UnityEngine;

public class RoomsController : MonoBehaviour
{
    public ElongatedRoomGenerator elongatedRoom;
    public ElongatedRoomGenerator secondElongatedRoom;

    Dictionary<string, TexturesStructure> textureCache;
    LinkedList<string> roomHistory;
    LinkedListNode<string> currentRoomNode;

    void Start()
    {
        textureCache = new Dictionary<string, TexturesStructure>();
        roomHistory = new LinkedList<string>();
        elongatedRoom.GenerateRoom(elongatedRoom.articleName, this);
        elongatedRoom.PreviousRoom = "";
        currentRoomNode = roomHistory.AddFirst(elongatedRoom.articleName);
    }

    public bool SwapRoomsNext()
    {
        if (currentRoomNode.Next == null)
        {
            Debug.LogWarning("No next room in history to swap to.");
            return false;
        }
        ElongatedRoomGenerator temp = elongatedRoom;
        elongatedRoom = secondElongatedRoom;
        secondElongatedRoom = temp;


        elongatedRoom.EnterTime = Time.time;
        secondElongatedRoom.ExitTime = Time.time;
        elongatedRoom.PreviousRoom = secondElongatedRoom.ArticleData.name;
        secondElongatedRoom.LogRoom();
        currentRoomNode = currentRoomNode.Next;
        return true;
    }

    public bool SwapRoomsPrevious()
    {
        if (currentRoomNode.Previous == null)
        {
            Debug.LogWarning("No previous room in history to swap to.");
            return false;
        }
        ElongatedRoomGenerator temp = elongatedRoom;
        elongatedRoom = secondElongatedRoom;
        secondElongatedRoom = temp;

        elongatedRoom.EnterTime = Time.time;
        secondElongatedRoom.ExitTime = Time.time;
        elongatedRoom.PreviousRoom = secondElongatedRoom.ArticleData.name;
        secondElongatedRoom.LogRoom();
        currentRoomNode = currentRoomNode.Previous;
        elongatedRoom.ResetRoom();
        elongatedRoom.GenerateRoom(currentRoomNode.Value, this);
        return true;
    }

    public TexturesStructure GetCachedTextures(string articleName)
    {
        if (textureCache.ContainsKey(articleName))
        {
            return textureCache[articleName];
        }
        return null;
    }

    public void CacheTextures(string articleName, TexturesStructure images)
    {
        if (!textureCache.ContainsKey(articleName))
        {
            textureCache[articleName] = images;
        }
    }

    public void AddNextRoomToHistory(string articleName)
    {
        while (currentRoomNode.Next != null)
        {
            roomHistory.RemoveLast();
        }
        roomHistory.AddAfter(currentRoomNode, articleName);
    }

}
