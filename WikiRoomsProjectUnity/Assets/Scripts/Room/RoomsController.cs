using System.Collections.Generic;
using UnityEngine;

public class RoomsController : MonoBehaviour
{
    public ElongatedRoomGenerator elongatedRoom;
    public ElongatedRoomGenerator secondElongatedRoom;

    Dictionary<string, TexturesStructure> textureCache;

    void Start()
    {
        textureCache = new Dictionary<string, TexturesStructure>();
        elongatedRoom.GenerateRoom(elongatedRoom.articleName, this);
        elongatedRoom.PreviousRoom = "";
    }

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

}
