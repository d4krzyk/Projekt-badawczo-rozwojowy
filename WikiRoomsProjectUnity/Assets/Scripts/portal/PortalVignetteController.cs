using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PortalVignetteController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScriptableRendererFeature _FullScreenBlizzard;
    [SerializeField] private Material _PortalVignetteMaterial;

    [Header("Settings")]
    [SerializeField] private float _vignetteIntensityMaxAmount = 20.0f;
    [SerializeField] private float _smoothSpeed = 50.0f;

    [Header("Tags")]
    [SerializeField] private string _portalTag = "Portal";
    [SerializeField] private string _playerTag = "Player";

    [Header("Placement")]
    [SerializeField] private bool _scriptIsOnPortal = false; // zaznacz, jeśli ten skrypt jest na obiekcie portalu

    [Header("Debug")]
    [SerializeField] private bool _logDebug = true;

    private int vignetteIntensity = Shader.PropertyToID("_VignetteIntensity");
    private const float VIGNETTE_INTENSITY_START_AMOUNT = 0.0f;

    private float _currentIntensity = VIGNETTE_INTENSITY_START_AMOUNT;
    private float _targetIntensity = VIGNETTE_INTENSITY_START_AMOUNT;
    private bool _featureEnabled = false;

    void Start()
    {
        if (_FullScreenBlizzard != null)
            _FullScreenBlizzard.SetActive(false);

        if (_PortalVignetteMaterial != null)
            _PortalVignetteMaterial.SetFloat(vignetteIntensity, VIGNETTE_INTENSITY_START_AMOUNT);

        Log($"Start. OnPortal={_scriptIsOnPortal}, portalTag='{_portalTag}', playerTag='{_playerTag}'. Mat={(_PortalVignetteMaterial ? _PortalVignetteMaterial.name : "NULL")}, Feature={(_FullScreenBlizzard ? _FullScreenBlizzard.name : "NULL")}");

        if (_PortalVignetteMaterial == null) LogWarning("Brak przypisanego materiału _PortalVignetteMaterial.");
        if (_FullScreenBlizzard == null) LogWarning("Brak przypisanego ScriptableRendererFeature _FullScreenBlizzard.");

        var col = GetComponent<Collider>();
        if (col == null) LogWarning("Brak Collider na tym obiekcie (Trigger wymagany).");
        else if (!col.isTrigger) LogWarning("Collider nie jest Triggerem (isTrigger = true).");

        if (GetComponent<Rigidbody>() == null)
            Log("Brak Rigidbody na tym obiekcie. Upewnij się, że co najmniej JEDEN z obiektów w kolizji ma Rigidbody (np. gracz lub portal).");
    }

    void Update()
    {
        SmoothVignette();
    }

    private void SmoothVignette()
    {
        if (_PortalVignetteMaterial == null || _FullScreenBlizzard == null)
            return;

        _currentIntensity = Mathf.MoveTowards(_currentIntensity, _targetIntensity, _smoothSpeed * Time.deltaTime);
        _PortalVignetteMaterial.SetFloat(vignetteIntensity, _currentIntensity);

        const float epsilon = 0.001f;
        if (!_featureEnabled && _currentIntensity > epsilon)
        {
            _FullScreenBlizzard.SetActive(true);
            _featureEnabled = true;
            Log("Włączono efekt (renderer feature ON).");
        }
        else if (_featureEnabled && _currentIntensity <= epsilon)
        {
            _FullScreenBlizzard.SetActive(false);
            _featureEnabled = false;
            Log("Wyłączono efekt (renderer feature OFF).");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        string expectedTag = _scriptIsOnPortal ? _playerTag : _portalTag;
        if (other.CompareTag(expectedTag))
        {
            _targetIntensity = _vignetteIntensityMaxAmount;
            Log($"OnTriggerEnter: wykryto '{other.name}' (tag='{expectedTag}') — uruchamiam efekt.");
        }
        else
        {
            Log($"OnTriggerEnter: ignoruję '{other.name}' (tag={other.tag}). Oczekiwano '{expectedTag}'.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        string expectedTag = _scriptIsOnPortal ? _playerTag : _portalTag;
        if (other.CompareTag(expectedTag))
        {
            Log($"OnTriggerExit: '{other.name}' (tag='{expectedTag}') — wygaszam efekt.");
            ExitPortal();
        }
    }

    private void ExitPortal()
    {
        _targetIntensity = VIGNETTE_INTENSITY_START_AMOUNT;
    }

    private void Log(string msg)
    {
        if (_logDebug) Debug.Log($"[PortalVignette] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (_logDebug) Debug.LogWarning($"[PortalVignette] {msg}", this);
    }

    // Przy wyłączeniu komponentu / zatrzymaniu gry wyłączamy efekt, żeby nie pozostał aktywny po End Play
    private void OnDisable()
    {
        if (_FullScreenBlizzard != null)
        {
            _FullScreenBlizzard.SetActive(false);
            _featureEnabled = false;
            if (_PortalVignetteMaterial != null)
                _PortalVignetteMaterial.SetFloat(vignetteIntensity, VIGNETTE_INTENSITY_START_AMOUNT);
            Log("OnDisable: wyłączono efekt (renderer feature OFF).");
        }
    }

    // Dodatkowo upewniamy się przy zamykaniu aplikacji
    private void OnApplicationQuit()
    {
        if (_FullScreenBlizzard != null)
        {
            _FullScreenBlizzard.SetActive(false);
            _featureEnabled = false;
            Log("OnApplicationQuit: wyłączono efekt (renderer feature OFF).");
        }
    }
}
