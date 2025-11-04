using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class Logger : MonoBehaviour
{
    string sessionId;
    List<Log> logs;


    void Start()
    {
        sessionId = Guid.NewGuid().ToString();
        logs = new List<Log>();
    }

    public void LogOnRoomExit(
        string roomName,
        float enterTime,
        float exitTime,
        List<BookLog> bookLogs,
        List<LinkLog> linkLogs,
        string previousRoom)
    {
        RoomLog log = new RoomLog();
        log.sessionId = sessionId;
        log.roomName = roomName;
        log.enterTime = enterTime;
        log.exitTime = exitTime;
        log.bookLogs = bookLogs;
        log.linkLogs = linkLogs;
        log.previousRoom = previousRoom;

        logs.Add(log);
        Debug.Log(JsonConvert.SerializeObject(logs));
    } 
}
