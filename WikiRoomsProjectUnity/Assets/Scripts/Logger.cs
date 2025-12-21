using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Networking;
using System.Threading.Tasks;

public class Logger : MonoBehaviour
{
    string sessionId;
    List<RoomLog> roomLogs;
    List<LinkLog> lastLinkLogs;
    List<BookLog> lastBookLogs;
    string currentPath;
    string playerNick;

    void Start()
    {
        sessionId = Guid.NewGuid().ToString();
        roomLogs = new List<RoomLog>();
        lastLinkLogs = new List<LinkLog>();
        lastBookLogs = new List<BookLog>();
        currentPath = "";
        playerNick = FindAnyObjectByType<GameController>().PlayerNick;
    }

    public void LogOnRoomExit(string roomName, float enterTime, float exitTime, string previousRoom)
    {
        RoomLog log = new RoomLog();
        // log.sessionId = sessionId;
        log.roomName = roomName;
        log.enterTime = enterTime;
        log.exitTime = exitTime;
        log.bookLogs = lastBookLogs;
        log.linkLogs = lastLinkLogs;
        // log.previousRoomLink = previousRoom;
        // log.roomPath = currentPath.Trim();

        roomLogs.Add(log);

        lastLinkLogs = new List<LinkLog>();
        lastBookLogs = new List<BookLog>();
        currentPath = "";
    }

    async void SendRoomLogToDB(AppLog logs)
    {
        string jsonData = JsonConvert.SerializeObject(logs);
        string url = "http://localhost/session";

        using (UnityWebRequest request = UnityWebRequest.Post(url, jsonData, "application/json"))
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("RoomLog sent successfully");
            }
            else
            {
                Debug.LogWarning($"Request error (sending RoomLog): {request.error}");
            }
        }
    }

    public void LogOnBookClose(string bookLink, float openTime, float closeTime)
    {
        BookLog log = new BookLog();
        log.bookName = bookLink;
        log.openTime = openTime;
        log.closeTime = closeTime;

        lastBookLogs.Add(log);
    }

    public void LogOnLinkClick(string linkName, float clickTime)
    {
        LinkLog log = new LinkLog();
        log.linkName = linkName;
        log.clickTime = clickTime;

        lastLinkLogs.Add(log);
    }

    public void UpdateCurrentPath(string currentMove)
    {
        currentPath += currentMove;
    }

    public void SendLogs()
    {
        AppLog logs = new AppLog();
        logs.user_name = playerNick;
        logs.session_logs = roomLogs;

        SendRoomLogToDB(logs);
    }
}
