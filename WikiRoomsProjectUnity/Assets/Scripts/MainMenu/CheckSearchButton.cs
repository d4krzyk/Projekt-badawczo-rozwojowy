using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class CheckSearchButton : MonoBehaviour
{
    public GameObject messagePlayPanelText;
    public GameObject nicknameTextField;
    public GameObject articleTextField;
    public GameObject targetArticleTextField;
    public Button SearchButton; // jeśli null, zostanie pobrany z tego GameObjectu lub z childów
    public GameController gameController;
    public BackendConfig backendConfig;

    [Header("API validation")]
    public bool requireTargetArticle = true;
    public bool useAuthorizationHeader = true;

    // cache TMP komponentów
    TextMeshProUGUI tmpUGUI;
    TextMeshPro tmp3D;

    bool isValidating;

    public async void CheckAndNotify()
    {   
        Debug.Log("[CheckSearchButton] CheckAndNotify()");

        if (isValidating)
            return;

        if (messagePlayPanelText == null)
        {
            Debug.LogWarning("[CheckSearchButton] messagePlayPanelText not assigned.");
            return;
        }

        EnsureMessageTextRefs();

        // // pobierz teksty z pól (obsługuje TMP_InputField, TextMeshProUGUI, TextMeshPro oraz InputField)
        string nick = (GetTextFromField(nicknameTextField) ?? string.Empty).Trim();
        string article = (GetTextFromField(articleTextField) ?? string.Empty).Trim();
        string targetArticle = (GetTextFromField(targetArticleTextField) ?? string.Empty).Trim();

        bool nickOk = !string.IsNullOrWhiteSpace(nick);
        bool articleOk = !string.IsNullOrWhiteSpace(article);
        bool targetOk = !requireTargetArticle || !string.IsNullOrWhiteSpace(targetArticle);

        if (!nickOk || !articleOk || !targetOk)
        {
            string msg;
            if (!nickOk && !articleOk) msg = "Please fill in nickname and starting article.";
            else if (!nickOk) msg = "Nickname is required.";
            else if (!articleOk) msg = "Starting article is required.";
            else msg = "Target article is required.";

            ShowMessage(msg);
            return;
        }

        if (!string.IsNullOrWhiteSpace(targetArticle) && string.Equals(article, targetArticle, StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage("Starting and target articles cannot be the same.");
            return;
        }

        if (gameController == null)
        {
            ShowMessage("GameController is missing in the scene. Cannot start.");
            return;
        }

        if (backendConfig == null || string.IsNullOrWhiteSpace(backendConfig.baseURL))
        {
            ShowMessage("Backend configuration is missing (BackendConfig).");
            return;
        }

        isValidating = true;
        SetSearchInteractable(false);
        ShowMessage("Validating data...");

        try
        {
            bool articleExists = await ValidateArticleExistsAsync(article);
            if (!articleExists)
            {
                ShowMessage($"Starting article '{article}' does not exist or is unavailable.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(targetArticle))
            {
                bool targetExists = await ValidateArticleExistsAsync(targetArticle);
                if (!targetExists)
                {
                    ShowMessage($"Target article '{targetArticle}' does not exist or is unavailable.");
                    return;
                }
            }

            NicknameValidationResult nickResult = await ValidateNicknameAvailabilityAsync(nick);
            if (!nickResult.success)
            {
                ShowMessage("Could not verify nickname availability. Please try again.");
                return;
            }

            if (!nickResult.available)
            {
                ShowMessage("This nickname is already taken.");
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CheckSearchButton] Validation error: {ex.Message}");
            ShowMessage("Validation error. Check your connection and try again.");
            return;
        }
        finally
        {
            isValidating = false;
            SetSearchInteractable(true);
        }

        gameController.ArticleName = article;
        gameController.PlayerNick = nick;
        gameController.TargetArticleName = targetArticle;
        // wszystko ok -> ukryj komunikat i wywołaj akcję startu (onClick przycisku)
        ClearMessage();
        messagePlayPanelText.SetActive(false);

        SceneManager.LoadScene("WikiRooms");
        // SceneManager.SetActiveScene(SceneManager.GetSceneByName("WikiRooms"));
    }

    // pomocnicza funkcja odczytująca tekst z różnych typów pól
    string GetTextFromField(GameObject go)
    {
        if (go == null) return null;

        var tmpInput = go.GetComponent<TMPro.TMP_InputField>();
        if (tmpInput != null) return tmpInput.text;
        return null;
    }

    void EnsureMessageTextRefs()
    {
        if (tmpUGUI == null && messagePlayPanelText != null)
            tmpUGUI = messagePlayPanelText.GetComponent<TextMeshProUGUI>();

        if (tmp3D == null && messagePlayPanelText != null)
            tmp3D = messagePlayPanelText.GetComponent<TextMeshPro>();
    }

    void ShowMessage(string message)
    {
        EnsureMessageTextRefs();
        if (tmpUGUI != null) tmpUGUI.text = message;
        else if (tmp3D != null) tmp3D.text = message;
        if (messagePlayPanelText != null) messagePlayPanelText.SetActive(true);
    }

    void ClearMessage()
    {
        EnsureMessageTextRefs();
        if (tmpUGUI != null) tmpUGUI.text = string.Empty;
        else if (tmp3D != null) tmp3D.text = string.Empty;
    }

    void SetSearchInteractable(bool interactable)
    {
        if (SearchButton == null)
        {
            SearchButton = GetComponent<Button>();
        }

        if (SearchButton != null)
            SearchButton.interactable = interactable;
    }

    async Task<bool> ValidateArticleExistsAsync(string article)
    {
        string encodedArticle = UnityWebRequest.EscapeURL(article);
        string url = $"{backendConfig.baseURL}/article?article={encodedArticle}&category_strategy=api";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("accept", "application/json");
            ApplyAuthHeaderIfConfigured(request);

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[CheckSearchButton] Article validation failed ({request.responseCode}): {request.error}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.downloadHandler.text))
                return false;

            try
            {
                var json = JObject.Parse(request.downloadHandler.text);
                JToken nameToken = json["name"];
                JToken contentToken = json["content"];
                return nameToken != null && contentToken != null;
            }
            catch
            {
                return false;
            }
        }
    }

    async Task<NicknameValidationResult> ValidateNicknameAvailabilityAsync(string nickname)
    {
        string encodedNick = UnityWebRequest.EscapeURL(nickname);
        string url = $"{backendConfig.baseURL}/session/check-user/{encodedNick}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("accept", "application/json");
            request.SetRequestHeader("X-Web", "false");
            ApplyAuthHeaderIfConfigured(request);

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[CheckSearchButton] Nickname validation failed ({request.responseCode}): {request.error}");
                return new NicknameValidationResult { success = false, available = false };
            }

            if (!TryParseUserExists(request.downloadHandler.text, out bool userExists))
            {
                Debug.LogWarning("[CheckSearchButton] Could not parse /session/check-user response.");
                return new NicknameValidationResult { success = false, available = false };
            }

            return new NicknameValidationResult { success = true, available = !userExists };
        }
    }

    bool TryParseUserExists(string responseText, out bool userExists)
    {
        userExists = false;
        if (string.IsNullOrWhiteSpace(responseText)) return false;

        string trimmed = responseText.Trim();
        if (bool.TryParse(trimmed, out bool rawBool))
        {
            userExists = rawBool;
            return true;
        }

        try
        {
            JToken token = JToken.Parse(trimmed);
            if (token.Type == JTokenType.Boolean)
            {
                userExists = token.Value<bool>();
                return true;
            }

            if (token.Type != JTokenType.Object) return false;
            JObject obj = (JObject)token;

            string[] existsKeys = { "exists", "taken", "is_taken", "isTaken", "occupied", "user_exists", "userExists" };
            foreach (string key in existsKeys)
            {
                if (obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken value) && value.Type == JTokenType.Boolean)
                {
                    userExists = value.Value<bool>();
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    void ApplyAuthHeaderIfConfigured(UnityWebRequest request)
    {
        if (!useAuthorizationHeader || backendConfig == null) return;
        if (string.IsNullOrWhiteSpace(backendConfig.username) || string.IsNullOrWhiteSpace(backendConfig.password)) return;

        string authHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(backendConfig.username + ":" + backendConfig.password));
        request.SetRequestHeader("Authorization", authHeader);
    }

    struct NicknameValidationResult
    {
        public bool success;
        public bool available;
    }



}
