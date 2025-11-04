using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public abstract class Log
{
    public string sessionId;
}

[Serializable]
public class RoomLog : Log
{
    public string roomName;
    public float enterTime;
    public float exitTime;
    public List<BookLog> bookLogs;
    public List<LinkLog> linkLogs;
    public string previousRoom;
}

[Serializable]
public class BookLog : Log
{
    public string bookName;
    public int openTime;
    public int closeTime;
}

[Serializable]
public class LinkLog : Log
{
    public string linkName;
    public int clickTime;
}