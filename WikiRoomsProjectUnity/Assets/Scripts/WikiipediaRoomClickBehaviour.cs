using UnityEngine;
using LogicUI.FancyTextRendering;
using System.Text.RegularExpressions;
using System;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(TextLinkHelper))]
[DisallowMultipleComponent]
public class WikiipediaRoomClickBehaviour : MonoBehaviour
{
    const string WikipediaArticlePattern = @"https:\/\/en\.wikipedia\.org\/wiki\/([^#]+)(?:#(.+))?";
    const string WikipediaHost = "wikipedia.org";
    const float MinimumExternalLinkMessageDurationSeconds = 3f;
    const float TooltipMaxWidth = 360f;
    static readonly Vector2 TooltipPadding = new Vector2(20f, 16f);
    static readonly Vector2 TooltipOffset = new Vector2(18f, -18f);

    RoomsController _roomsController;
    Logger logger;
    TextLinkHelper linkHelper;
    TMP_Text pageText;
    Canvas parentCanvas;

    static RectTransform tooltipRoot;
    static Image tooltipBackground;
    static TextMeshProUGUI tooltipLabel;
    static Canvas tooltipCanvas;
    static WikiipediaRoomClickBehaviour activeTooltipOwner;
    static Sprite tooltipFallbackSprite;
    static GameObject cachedFullScreenTextObject;

    [Header("SFX kliknięcia linku")]
    public AudioClip clickSound;
    [Range(0f, 1f)]
    public float clickVolume = 0.8f;

    [Header("Pozycja użytkownika (opcjonalnie)")]
    public Transform userTransform; // ustaw np. Transform gracza lub kamery

    [Header("External links")]
    [Tooltip("Optional. If empty, script will auto-find object named 'FullScreenText' in loaded scenes.")]
    public GameObject fullScreenTextObject;
    [Min(3f)] public float externalLinkMessageDuration = 3f;
    [Min(0f)] public float externalLinkOpenDelaySeconds = 0.1f;

    Coroutine externalLinkStatusRoutine;

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
        linkHelper = GetComponent<TextLinkHelper>();
        pageText = GetComponent<TMP_Text>();

        if (userTransform == null)
        {
            try
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    userTransform = player.transform;
            }
            catch (UnityException)
            {
            }
        }

        if (linkHelper != null)
        {
            linkHelper.OnLinkClicked += ClickOnLink;
            linkHelper.OnLinkHovered += ShowLinkTooltip;
            linkHelper.OnLinkHoverEnded += HideLinkTooltip;
        }

        logger = FindAnyObjectByType<Logger>();

        EnsureFullScreenTextReference();
        if (fullScreenTextObject != null)
            fullScreenTextObject.SetActive(false);
    }

    private void OnValidate()
    {
        if (externalLinkMessageDuration < MinimumExternalLinkMessageDurationSeconds)
            externalLinkMessageDuration = MinimumExternalLinkMessageDurationSeconds;

        if (externalLinkOpenDelaySeconds < 0f)
            externalLinkOpenDelaySeconds = 0f;
    }

    private void Update()
    {
        if (activeTooltipOwner == this)
        {
            UpdateTooltipPosition();
        }
    }

    private void OnDisable()
    {
        if (activeTooltipOwner == this)
        {
            HideTooltipImmediate();
        }

        HideExternalLinkStatusImmediate();
    }

    private void OnDestroy()
    {
        if (linkHelper != null)
        {
            linkHelper.OnLinkClicked -= ClickOnLink;
            linkHelper.OnLinkHovered -= ShowLinkTooltip;
            linkHelper.OnLinkHoverEnded -= HideLinkTooltip;
        }

        if (activeTooltipOwner == this)
        {
            HideTooltipImmediate();
        }

        HideExternalLinkStatusImmediate();
    }

    private void ClickOnLink(string link)
    {
        HideLinkTooltip();

        // Odtwórz dźwięk w lokalizacji użytkownika
        PlayClickSoundAtUser();

        link = NormalizeLink(link);

        logger.LogOnLinkClick(link, Time.time);

        if (TryGetWikipediaArticleName(link, out string articleName))
        {
            PlayerController playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                playerController.CloseReadingView(false);
            }

            roomsController.secondElongatedRoom.articleName = articleName;
            roomsController.secondElongatedRoom.ResetRoom();
            roomsController.elongatedRoom.SetActivePortalNext(false);
            roomsController.secondElongatedRoom.GenerateRoom(roomsController.secondElongatedRoom.articleName, roomsController, onClick: true);
            roomsController.AddNextRoomToHistory(roomsController.secondElongatedRoom.articleName);
            return;
        }

        if (TryGetExternalUrl(link, out string externalUrl))
        {
            ShowExternalLinkStatus();
            StartCoroutine(OpenExternalUrlWithDelay(externalUrl));
        }
    }

    private void ShowLinkTooltip(string link)
    {
        string articleTitle = ExtractArticleTitle(link);
        if (string.IsNullOrEmpty(articleTitle))
        {
            HideLinkTooltip();
            return;
        }

        EnsureTooltipExists();
        if (tooltipRoot == null || tooltipLabel == null)
            return;

        activeTooltipOwner = this;
        tooltipLabel.text = articleTitle;
        UpdateTooltipStyle();
        UpdateTooltipSize();
        tooltipRoot.gameObject.SetActive(true);
        tooltipRoot.SetAsLastSibling();
        UpdateTooltipPosition();
    }

    private void HideLinkTooltip()
    {
        if (activeTooltipOwner != null && activeTooltipOwner != this)
            return;

        HideTooltipImmediate();
    }

    private void HideTooltipImmediate()
    {
        if (tooltipRoot != null)
            tooltipRoot.gameObject.SetActive(false);

        activeTooltipOwner = null;
    }

    private void EnsureTooltipExists()
    {
        Canvas canvas = GetTooltipCanvas();
        if (canvas == null)
            return;

        if (tooltipRoot != null && tooltipCanvas == canvas)
            return;

        if (tooltipRoot != null)
        {
            Destroy(tooltipRoot.gameObject);
        }

        tooltipCanvas = canvas;

        GameObject tooltipObject = new GameObject("LinkHoverTooltip", typeof(RectTransform), typeof(Image));
        tooltipObject.transform.SetParent(canvas.transform, false);

        tooltipRoot = tooltipObject.GetComponent<RectTransform>();
        tooltipRoot.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRoot.pivot = new Vector2(0f, 1f);

        tooltipBackground = tooltipObject.GetComponent<Image>();
        tooltipBackground.sprite = GetTooltipBackgroundSprite();
        tooltipBackground.type = Image.Type.Simple;
        tooltipBackground.color = new Color(0.09f, 0.11f, 0.15f, 0.94f);
        tooltipBackground.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(tooltipObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(TooltipPadding.x * 0.5f, TooltipPadding.y * 0.5f);
        labelRect.offsetMax = new Vector2(-TooltipPadding.x * 0.5f, -TooltipPadding.y * 0.5f);

        tooltipLabel = labelObject.GetComponent<TextMeshProUGUI>();
        tooltipLabel.textWrappingMode = TextWrappingModes.Normal;
        tooltipLabel.overflowMode = TextOverflowModes.Overflow;
        tooltipLabel.alignment = TextAlignmentOptions.Left;
        tooltipLabel.raycastTarget = false;
        tooltipLabel.text = string.Empty;

        UpdateTooltipStyle();
        tooltipRoot.gameObject.SetActive(false);
    }

    private static Sprite GetTooltipBackgroundSprite()
    {
        if (tooltipFallbackSprite != null)
            return tooltipFallbackSprite;

        Texture2D texture = Texture2D.whiteTexture;
        tooltipFallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f));

        return tooltipFallbackSprite;
    }

    private Canvas GetTooltipCanvas()
    {
        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
                parentCanvas = FindAnyObjectByType<Canvas>();
        }

        return parentCanvas;
    }

    private void UpdateTooltipStyle()
    {
        if (tooltipLabel == null || pageText == null)
            return;

        if (pageText.font != null)
            tooltipLabel.font = pageText.font;

        tooltipLabel.fontSize = Mathf.Max(20f, pageText.fontSize * 0.55f);
        tooltipLabel.color = Color.white;
    }

    private void UpdateTooltipSize()
    {
        if (tooltipRoot == null || tooltipLabel == null)
            return;

        Vector2 preferredSize = tooltipLabel.GetPreferredValues(tooltipLabel.text, TooltipMaxWidth, 0f);
        float width = Mathf.Min(TooltipMaxWidth, preferredSize.x) + TooltipPadding.x;
        float height = preferredSize.y + TooltipPadding.y;

        tooltipRoot.sizeDelta = new Vector2(width, height);
    }

    private void UpdateTooltipPosition()
    {
        if (tooltipRoot == null || tooltipCanvas == null || !tooltipRoot.gameObject.activeSelf)
            return;

        RectTransform canvasRect = tooltipCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Camera eventCamera = tooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : tooltipCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, eventCamera, out Vector2 localPoint))
            return;

        Vector2 targetPosition = localPoint + TooltipOffset;
        Vector2 tooltipSize = tooltipRoot.rect.size;
        Rect canvasBounds = canvasRect.rect;

        targetPosition.x = Mathf.Clamp(targetPosition.x, canvasBounds.xMin, canvasBounds.xMax - tooltipSize.x);
        targetPosition.y = Mathf.Clamp(targetPosition.y, canvasBounds.yMin + tooltipSize.y, canvasBounds.yMax);

        tooltipRoot.anchoredPosition = targetPosition;
    }

    private string ExtractArticleTitle(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return string.Empty;

        string normalizedLink = NormalizeLink(link);
        Match match = Regex.Match(normalizedLink, WikipediaArticlePattern);
        if (!match.Success)
            return normalizedLink;

        string rawTitle = match.Groups[1].Value.Replace('_', ' ');
        return Uri.UnescapeDataString(rawTitle);
    }

    private string NormalizeLink(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return string.Empty;

        string normalized = CloseParentheses(link.Trim());
        if (normalized.StartsWith("//", StringComparison.Ordinal))
            normalized = "https:" + normalized;

        if (normalized.StartsWith("/wiki/", StringComparison.OrdinalIgnoreCase))
            normalized = "https://en.wikipedia.org" + normalized;

        return normalized;
    }

    private bool TryGetWikipediaArticleName(string link, out string articleName)
    {
        articleName = string.Empty;
        if (string.IsNullOrWhiteSpace(link))
            return false;

        Match directMatch = Regex.Match(link, WikipediaArticlePattern);
        if (directMatch.Success)
        {
            articleName = Uri.UnescapeDataString(directMatch.Groups[1].Value.Replace('_', ' '));
            return true;
        }

        if (!Uri.TryCreate(link, UriKind.Absolute, out Uri uri))
            return false;

        if (!IsWikipediaHost(uri.Host))
            return false;

        if (!uri.AbsolutePath.StartsWith("/wiki/", StringComparison.OrdinalIgnoreCase))
            return false;

        string rawTitle = uri.AbsolutePath.Substring("/wiki/".Length);
        if (string.IsNullOrWhiteSpace(rawTitle))
            return false;

        articleName = Uri.UnescapeDataString(rawTitle.Replace('_', ' '));
        return true;
    }

    private bool TryGetExternalUrl(string link, out string externalUrl)
    {
        externalUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(link))
            return false;

        if (!Uri.TryCreate(link, UriKind.Absolute, out Uri uri))
            return false;

        bool isHttp = uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!isHttp)
            return false;

        if (IsWikipediaHost(uri.Host))
            return false;

        externalUrl = uri.AbsoluteUri;
        return true;
    }

    private bool IsWikipediaHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        return host.Equals(WikipediaHost, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + WikipediaHost, StringComparison.OrdinalIgnoreCase);
    }

    private void ShowExternalLinkStatus()
    {
        EnsureFullScreenTextReference();

        if (fullScreenTextObject == null)
        {
            Debug.Log("Opening external link...");
            return;
        }

        if (externalLinkStatusRoutine != null)
            StopCoroutine(externalLinkStatusRoutine);

        externalLinkStatusRoutine = StartCoroutine(ShowExternalLinkStatusRoutine());
    }

    private IEnumerator ShowExternalLinkStatusRoutine()
    {
        fullScreenTextObject.SetActive(true);

        float messageDuration = GetEffectiveExternalLinkMessageDurationSeconds();
        if (messageDuration > 0f)
            yield return new WaitForSeconds(messageDuration);

        if (fullScreenTextObject != null)
            fullScreenTextObject.SetActive(false);

        externalLinkStatusRoutine = null;
    }

    private IEnumerator OpenExternalUrlWithDelay(string externalUrl)
    {
        float messageDuration = GetEffectiveExternalLinkMessageDurationSeconds();
        if (messageDuration > 0f)
            yield return new WaitForSeconds(messageDuration);

        if (externalLinkOpenDelaySeconds > 0f)
            yield return new WaitForSeconds(externalLinkOpenDelaySeconds);

        Application.OpenURL(externalUrl);
    }

    private float GetEffectiveExternalLinkMessageDurationSeconds()
    {
        return Mathf.Max(MinimumExternalLinkMessageDurationSeconds, externalLinkMessageDuration);
    }

    private void HideExternalLinkStatusImmediate()
    {
        if (externalLinkStatusRoutine != null)
        {
            StopCoroutine(externalLinkStatusRoutine);
            externalLinkStatusRoutine = null;
        }

        if (fullScreenTextObject != null)
            fullScreenTextObject.SetActive(false);
    }

    private void EnsureFullScreenTextReference()
    {
        if (fullScreenTextObject != null)
            return;

        if (cachedFullScreenTextObject != null)
        {
            fullScreenTextObject = cachedFullScreenTextObject;
            return;
        }

        fullScreenTextObject = FindObjectByNameInLoadedScenes("FullScreenText");
        if (fullScreenTextObject != null)
            cachedFullScreenTextObject = fullScreenTextObject;
    }

    private static GameObject FindObjectByNameInLoadedScenes(string objectName)
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject found = FindInHierarchyByName(roots[i].transform, objectName);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private static GameObject FindInHierarchyByName(Transform root, string objectName)
    {
        if (root == null)
            return null;

        if (root.name.Equals(objectName, StringComparison.Ordinal))
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindInHierarchyByName(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
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

    string CloseParentheses(string link)
    {
        string newLink = String.Copy(link);
        int openParantheses = 0;
        foreach(var c in link)
        {
            if (c == '(') openParantheses++;
            else if (c == ')') openParantheses--;
        }
        for(int i = 0; i < openParantheses; i++)
        {
            newLink += ')';
        }
        return newLink;
    }
}
