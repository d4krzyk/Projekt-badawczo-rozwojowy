using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class CheckSearchButton : MonoBehaviour
{
    public GameObject messagePlayPanelText;
    public GameObject nicknameTextField;
    public GameObject articleTextField;
    public Button SearchButton; // jeśli null, zostanie pobrany z tego GameObjectu lub z childów
    public GameController gameController;

    // cache TMP komponentów
    TextMeshProUGUI tmpUGUI;
    TextMeshPro tmp3D;

    
    public void CheckAndNotify()
    {   
        Debug.Log("[CheckSearchButton] CheckAndNotify()");

        if (messagePlayPanelText == null)
        {
            Debug.LogWarning("[CheckSearchButton] messagePlayPanelText nie przypisany.");
            return;
        }

        // // pobierz teksty z pól (obsługuje TMP_InputField, TextMeshProUGUI, TextMeshPro oraz InputField)
        string nick = GetTextFromField(nicknameTextField);
        string article = GetTextFromField(articleTextField);

        bool nickOk = !string.IsNullOrWhiteSpace(nick);
        bool articleOk = !string.IsNullOrWhiteSpace(article);

        var tmpUGUI = messagePlayPanelText.GetComponent<TMPro.TextMeshProUGUI>();
        var tmp3D = messagePlayPanelText.GetComponent<TMPro.TextMeshPro>();

        if (!nickOk || !articleOk)
        {
            string msg;
            if (!nickOk && !articleOk) msg = "Wypełnij wszystkie pola";
            else if (!nickOk) msg = "Wypełnij pole Nickname";
            else msg = "Wypełnij pole Article";

            if (tmpUGUI != null) tmpUGUI.text = msg;
            else if (tmp3D != null) tmp3D.text = msg;
            messagePlayPanelText.SetActive(true);
            return;
        }
        gameController.ArticleName = article;
        // wszystko ok -> ukryj komunikat i wywołaj akcję startu (onClick przycisku)
        if (tmpUGUI != null) tmpUGUI.text = "";
        else if (tmp3D != null) tmp3D.text = "";
        messagePlayPanelText.SetActive(false);

        SceneManager.LoadScene("WikiRooms");
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("WikiRooms"));

        return;
    }

    // pomocnicza funkcja odczytująca tekst z różnych typów pól
    string GetTextFromField(GameObject go)
    {
        if (go == null) return null;

        var tmpInput = go.GetComponent<TMPro.TMP_InputField>();
        if (tmpInput != null) return tmpInput.text;
        return null;
    }



}
