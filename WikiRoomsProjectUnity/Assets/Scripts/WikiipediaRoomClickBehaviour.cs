using UnityEngine;
using LogicUI.FancyTextRendering;
using System.Text.RegularExpressions;

[RequireComponent(typeof(TextLinkHelper))]
[DisallowMultipleComponent]
public class WikiipediaRoomClickBehaviour : MonoBehaviour
{
    RoomsController _roomsController;
    Logger logger;

    [Header("SFX kliknięcia linku")]
    public AudioClip clickSound;
    [Range(0f, 1f)]
    public float clickVolume = 0.8f;

    [Header("Pozycja użytkownika (opcjonalnie)")]
    public Transform userTransform; // ustaw np. Transform gracza lub kamery

    public RoomsController roomsController
    {
        get
        {
            if (_roomsController == null)
                _roomsController = FindAnyObjectByType<RoomsController>();
            return _roomsController;
        }
    }

    private void Awake()
    {
        GetComponent<TextLinkHelper>().OnLinkClicked += ClickOnLink;
        logger = FindAnyObjectByType<Logger>();
    }

    private void ClickOnLink(string link)
    {
        // Odtwórz dźwięk w lokalizacji użytkownika
        PlayClickSoundAtUser();

        string pattern = @"https:\/\/en\.wikipedia\.org\/wiki\/([^#]+)(?:#(.+))?";

        logger.LogOnLinkClick(link, Time.time);

        Match match = Regex.Match(link, pattern);
        if (match.Success)
        {
            roomsController.secondElongatedRoom.articleName = match.Groups[1].Value.Replace('_', ' ');
            roomsController.secondElongatedRoom.ResetRoom();
            roomsController.secondElongatedRoom.GenerateRoom(roomsController.secondElongatedRoom.articleName, roomsController);
            roomsController.AddNextRoomToHistory(roomsController.secondElongatedRoom.articleName);
        }
    }

    private void PlayClickSoundAtUser()
    {
        if (clickSound == null) return;

        Vector3 pos;
        if (userTransform != null)
        {
            pos = userTransform.position;
        }
        else if (Camera.main != null)
        {
            pos = Camera.main.transform.position;
        }
        else
        {
            pos = Vector3.zero;
        }

        AudioSource.PlayClipAtPoint(clickSound, pos, clickVolume);
    }
}
