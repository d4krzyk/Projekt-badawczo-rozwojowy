using System.Collections.Generic;
using UnityEngine;

public class RoomsController : MonoBehaviour
{
    public class CachedImageData
    {
        public Texture2D texture;
        public string caption;
    }

    public ElongatedRoomGenerator elongatedRoom;
    public ElongatedRoomGenerator secondElongatedRoom;
    public GameObject finalUI;
    public TMPro.TMP_Text finalUIScore;
    public TMPro.TMP_Text finalUIRoomCount;
    public TMPro.TMP_Text finalUITime;
    public TMPro.TMP_Text goalUI;
    public Logger logger;

    Dictionary<string, TexturesStructure> textureCache;
    Dictionary<string, ArticleStructure> articleCache;
    Dictionary<string, List<CachedImageData>> imageCache;
    LinkedList<string> roomHistory;
    LinkedListNode<string> currentRoomNode;
    string targetArticleName = null;
    bool sessionEnded = false;

    void Start()
    {
        AudioListener.pause = false;

        textureCache = new Dictionary<string, TexturesStructure>();
        articleCache = new Dictionary<string, ArticleStructure>();
        imageCache = new Dictionary<string, List<CachedImageData>>();
        roomHistory = new LinkedList<string>();
        elongatedRoom.GenerateRoom(elongatedRoom.articleName, this);
        elongatedRoom.PreviousRoom = "";
        currentRoomNode = roomHistory.AddFirst(elongatedRoom.articleName);
        targetArticleName = FindAnyObjectByType<GameController>()?.TargetArticleName;
        goalUI.text = targetArticleName != null ? targetArticleName : "No target article set";
    }

    public bool SwapRoomsNext()
    {
        if (sessionEnded)
        {
            return false;
        }

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
        if (currentRoomNode.Value != elongatedRoom.articleName)
        {
            elongatedRoom.ResetRoom();
            elongatedRoom.GenerateRoom(currentRoomNode.Value, this);
        }
        if (!elongatedRoom.HasLoaded)
        {
            elongatedRoom.loadingScreen.SetActive(true);
            // Zresetuj animację loading screen
            var loadingMotion = elongatedRoom.loadingScreen.GetComponentInChildren<LoadingPuzzleMotion>();
            if (loadingMotion != null) loadingMotion.ResetAnimation();
        }
        if(targetArticleName != null)
        {
            if(elongatedRoom.ArticleData.name.ToLower() == targetArticleName.ToLower())
            {
                EnterFinalState();
                finalUI.SetActive(true);
                finalUIScore.text = logger.GetTotalBooksOpened().ToString("D9");
                finalUIRoomCount.text = logger.GetTotalRoomsVisited().ToString("D9");
                int duration = (int)logger.GetSessionDuration();
                int hours = Mathf.FloorToInt(duration / 3600);
                int minutes = Mathf.FloorToInt(duration / 60 % 60);
                int seconds = Mathf.FloorToInt(duration % 60);
                finalUITime.text = $"{hours:00} h {minutes:00} min {seconds:00} s";
                logger.SendLogs();
            }
        }
        elongatedRoom.SetActivePortalPrevious(true);
        if (currentRoomNode.Next != null) elongatedRoom.SetActivePortalNext(true);
        else elongatedRoom.SetActivePortalNext(false);
        return true;
    }

    public bool SwapRoomsPrevious()
    {
        if (sessionEnded)
        {
            return false;
        }

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
        elongatedRoom.SetActivePortalNext(true);
        if (currentRoomNode.Previous != null) elongatedRoom.SetActivePortalPrevious(true);
        else elongatedRoom.SetActivePortalPrevious(false);
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

    public ArticleStructure GetCachedArticle(string articleName)
    {
        if (articleCache.ContainsKey(articleName))
        {
            return articleCache[articleName];
        }
        return null;
    }

    public void CacheArticle(string articleName, ArticleStructure article)
    {
        if (!articleCache.ContainsKey(articleName))
        {
            articleCache[articleName] = article;
        }
    }

    public List<CachedImageData> GetCachedImages(string articleName)
    {
        if (imageCache.TryGetValue(articleName, out List<CachedImageData> images))
        {
            return images;
        }

        return null;
    }

    public void CacheImages(string articleName, List<CachedImageData> images)
    {
        if (string.IsNullOrEmpty(articleName) || images == null || images.Count == 0)
        {
            return;
        }

        imageCache[articleName] = images;
    }

    public void AddNextRoomToHistory(string articleName)
    {
        while (currentRoomNode.Next != null)
        {
            roomHistory.RemoveLast();
        }
        roomHistory.AddAfter(currentRoomNode, articleName);
        elongatedRoom.SetActivePortalNext(true);
    }

    // Zwraca nazwę następnego generatora pokoju (prosta wersja)
    public string GetNextRoomName()
    {
        return secondElongatedRoom != null ? secondElongatedRoom.articleName : null;
    }
    public string GetPreviousRoomName()
    {
        return currentRoomNode.Previous != null ? currentRoomNode.Previous.Value : null;
    }

    void EnterFinalState()
    {
        if (sessionEnded) return;
        sessionEnded = true;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        PlayerController playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerController.movementLocked = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
