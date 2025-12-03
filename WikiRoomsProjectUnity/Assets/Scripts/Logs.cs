using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public abstract class Log
{
    public string sessionId;
}

[Serializable]
public class RoomLog
{
    public string roomName;
    public float enterTime;
    public float exitTime;
    public List<BookLog> bookLogs;
    public List<LinkLog> linkLogs;
    // public string previousRoomLink;
    // public string roomPath;
}

[Serializable]
public class BookLog
{
    public string bookName;
    public float openTime;
    public float closeTime;
}

[Serializable]
public class LinkLog
{
    public string linkName;
    public float clickTime;
}

[Serializable]
public class AppLog
{
    public string user_name;
    public List<RoomLog> session_logs;
}