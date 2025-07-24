using UnityEngine;
using LogicUI.FancyTextRendering;
using System.Text.RegularExpressions;

[RequireComponent(typeof(TextLinkHelper))]
[DisallowMultipleComponent]
public class WikiipediaRoomClickBehaviour : MonoBehaviour
{
    RoomsController _roomsController;
    public RoomsController roomsController
    {
        get
        {
            if (_roomsController == null)
                _roomsController = FindObjectOfType<RoomsController>();
            return _roomsController;
        }
    }

    private void Awake()
    {
        GetComponent<TextLinkHelper>().OnLinkClicked += ClickOnLink;
    }


    private void ClickOnLink(string link)
    {
        string pattern = @"https:\/\/en\.wikipedia\.org\/wiki\/([^#]+)(?:#(.+))?";


        Match match = Regex.Match(link, pattern);
        if (match.Success)
        {
            Debug.Log("Page Title: " + match.Groups[1].Value); // Head_of_the_Church
            Debug.Log("Anchor: " + match.Groups[2].Value);     // Catholic_Church
            roomsController.secondRoom.articleName = match.Groups[1].Value.Replace('_', ' ');
            roomsController.secondRoom.ResetRoom();
            roomsController.secondRoom.GenerateRoom(roomsController.secondRoom.articleName);
        }
    }
}
