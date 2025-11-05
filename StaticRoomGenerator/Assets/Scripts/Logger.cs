using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class Logger : MonoBehaviour
{
    string sessionId;
    List<Log> logs;
    List<LinkLog> lastLinkLogs;
    List<BookLog> lastBookLogs;


    void Start()
    {
        sessionId = Guid.NewGuid().ToString();
        logs = new List<Log>();
        lastLinkLogs = new List<LinkLog>();
        lastBookLogs = new List<BookLog>();
    }

    public void LogOnRoomExit(
        string roomName,
        float enterTime,
        float exitTime,
        string previousRoom
        )
    {
        RoomLog log = new RoomLog();
        log.sessionId = sessionId;
        log.roomName = roomName;
        log.enterTime = enterTime;
        log.exitTime = exitTime;
        log.bookLogs = lastBookLogs;
        log.linkLogs = lastLinkLogs;
        log.previousRoom = previousRoom;

        logs.Add(log);

        lastLinkLogs = new List<LinkLog>();
        lastBookLogs = new List<BookLog>();

        Debug.Log(JsonConvert.SerializeObject(logs));
    }

    public void LogOnBookClose(string bookName, float openTime, float closeTime)
    {
        BookLog log = new BookLog();
        log.bookName = bookName;
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
}
