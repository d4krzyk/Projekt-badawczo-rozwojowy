using UnityEngine;
using LogicUI.FancyTextRendering;
using System.Text.RegularExpressions;

[RequireComponent(typeof(TextLinkHelper))]
[DisallowMultipleComponent]
public class WikiipediaRoomClickBehaviour : MonoBehaviour
{
    RoomsController _roomsController;
    Logger logger;
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
        string pattern = @"https:\/\/en\.wikipedia\.org\/wiki\/([^#]+)(?:#(.+))?";

        logger.LogOnLinkClick(link, Time.time);

        Match match = Regex.Match(link, pattern);
        if (match.Success)
        {
            roomsController.secondRoom.articleName = match.Groups[1].Value.Replace('_', ' ');
            roomsController.secondRoom.ResetRoom();
            roomsController.secondRoom.GenerateRoom(roomsController.secondRoom.articleName);
        }
    }
}
