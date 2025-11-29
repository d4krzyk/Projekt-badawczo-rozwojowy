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
    string currentPath;

    void Start()
    {
        sessionId = Guid.NewGuid().ToString();
        logs = new List<Log>();
        lastLinkLogs = new List<LinkLog>();
        lastBookLogs = new List<BookLog>();
        currentPath = "";
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
        log.roomLink = roomName;
        log.enterTime = enterTime;
        log.exitTime = exitTime;
        log.bookLogs = lastBookLogs;
        log.linkLogs = lastLinkLogs;
        log.previousRoomLink = previousRoom;
        log.roomPath = currentPath.Trim();

        logs.Add(log);

        lastLinkLogs = new List<LinkLog>();
        lastBookLogs = new List<BookLog>();
        currentPath = "";

        Debug.Log(JsonConvert.SerializeObject(logs));
    }

    public void LogOnBookClose(string bookLink, float openTime, float closeTime)
    {
        BookLog log = new BookLog();
        log.bookLink = bookLink;
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
}
