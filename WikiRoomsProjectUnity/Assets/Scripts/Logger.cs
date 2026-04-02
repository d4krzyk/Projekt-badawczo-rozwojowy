using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Zachowuje lokalne eventy gameplayowe po usunięciu zależności od backendowego
/// /session oraz całej warstwy analityczno-badawczej.
/// 
/// Zamiast wysyłki do API zapisuje log sesji lokalnie w Application.persistentDataPath.
/// </summary>
public class Logger : MonoBehaviour
{
    [Header("Legacy backend config (unused)")]
    public BackendConfig backendConfig;

    [Header("Local log export")]
    public bool saveLogsToDisk = true;
    public string localLogsDirectoryName = "WikiRoomsSessionLogs";

    string sessionId;
    List<RoomLog> roomLogs;
    List<LinkLog> lastLinkLogs;
    List<BookLog> lastBookLogs;
    string currentPath;
    string playerNick;
    float sessionStartTime;

    public string LastSavedLogPath { get; private set; }

    void Start()
    {
        sessionStartTime = Time.time;
        sessionId = Guid.NewGuid().ToString();
        roomLogs = new List<RoomLog>();
        lastLinkLogs = new List<LinkLog>();
        lastBookLogs = new List<BookLog>();
        currentPath = string.Empty;

        GameController gameController = FindAnyObjectByType<GameController>();
        playerNick = gameController != null ? gameController.PlayerNick : "test_user";
    }

    public void LogOnRoomExit(string roomName, float enterTime, float exitTime, string previousRoom)
    {
        RoomLog log = new RoomLog
        {
            roomName = roomName,
            enterTime = enterTime,
            exitTime = exitTime,
            bookLogs = new List<BookLog>(lastBookLogs),
            linkLogs = new List<LinkLog>(lastLinkLogs),
        };

        roomLogs.Add(log);
        lastLinkLogs = new List<LinkLog>();
        lastBookLogs = new List<BookLog>();
        currentPath = string.Empty;
    }

    public void LogOnBookClose(string bookLink, float openTime, float closeTime)
    {
        lastBookLogs.Add(new BookLog
        {
            bookName = bookLink,
            openTime = openTime,
            closeTime = closeTime,
        });
    }

    public void LogOnLinkClick(string linkName, float clickTime)
    {
        lastLinkLogs.Add(new LinkLog
        {
            linkName = linkName,
            clickTime = clickTime,
        });
    }

    public void UpdateCurrentPath(string currentMove)
    {
        currentPath += currentMove;
    }

    public void SendLogs(bool surrender)
    {
        AppLog logs = new AppLog
        {
            user_name = playerNick,
            session_logs = roomLogs,
            group = "local",
            surrendered = surrender,
        };

        _ = SaveLogsLocallyAsync(logs);
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
        foreach (RoomLog room in roomLogs)
            count += room.bookLogs.Count;

        return count;
    }

    async Task SaveLogsLocallyAsync(AppLog logs)
    {
        if (!saveLogsToDisk || logs == null)
            return;

        try
        {
            string directoryPath = Path.Combine(Application.persistentDataPath, localLogsDirectoryName);
            Directory.CreateDirectory(directoryPath);

            string safeUser = string.IsNullOrWhiteSpace(playerNick) ? "player" : SanitizeFileName(playerNick);
            string fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{safeUser}_{sessionId}.json";
            string fullPath = Path.Combine(directoryPath, fileName);

            string jsonData = JsonConvert.SerializeObject(logs, Formatting.Indented);
            await File.WriteAllTextAsync(fullPath, jsonData);

            LastSavedLogPath = fullPath;
            Debug.Log($"[Logger] Session log saved locally: {fullPath}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Logger] Failed to save local session log: {exception.Message}");
        }
    }

    string SanitizeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "player";

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            raw = raw.Replace(invalidChar, '_');

        return raw.Trim();
    }
}
