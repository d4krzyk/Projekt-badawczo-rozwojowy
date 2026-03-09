using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Text;

public class Logger : MonoBehaviour
{
    public BackendConfig backendConfig;
    string sessionId;
    List<RoomLog> roomLogs;
    List<LinkLog> lastLinkLogs;
    List<BookLog> lastBookLogs;
    string currentPath;
    string playerNick;
    string auth_header;
    float sessionStartTime;

    void Start()
    {
        sessionStartTime = Time.time;
        sessionId = Guid.NewGuid().ToString();
        roomLogs = new List<RoomLog>();
        lastLinkLogs = new List<LinkLog>();
        lastBookLogs = new List<BookLog>();
        currentPath = "";
        if (FindAnyObjectByType<GameController>() != null) playerNick = FindAnyObjectByType<GameController>().PlayerNick;
        else playerNick = "test_user";
        auth_header = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(backendConfig.username + ":" + backendConfig.password));
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

    async void SendRoomLogToDB(AppLog logs, bool xWeb = false)
    {
        string jsonData = JsonConvert.SerializeObject(logs);
        Debug.Log($"Sending JSON: {jsonData}");
        string url = $"{backendConfig.baseURL}/session/"; // <-- trailing slash avoids 307

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-web", xWeb ? "true" : "false");
            request.SetRequestHeader("Authorization", auth_header);

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            Debug.Log($"Response code: {request.responseCode}; Body: {request.downloadHandler.text}");

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

    public void SendLogs(bool surrender)
    {
        AppLog logs = new AppLog();
        logs.user_name = playerNick;
        logs.session_logs = roomLogs;
        logs.group = "1";
        logs.surrendered = surrender;

        SendRoomLogToDB(logs, false); // ustaw true/false wg potrzeby
    }

    public float GetSessionDuration()
    {
        return Time.time - sessionStartTime;
    }

    public int GetTotalRoomsVisited()
    {
        return roomLogs.Count;
    }

    public int GetTotalBooksOpened()
    {
        int count = 0;
        foreach (var room in roomLogs)
        {
            count += room.bookLogs.Count;
        }
        return count;
    }
}
