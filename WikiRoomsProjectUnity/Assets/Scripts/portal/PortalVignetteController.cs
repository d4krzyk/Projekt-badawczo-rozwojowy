using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PortalVignetteController : MonoBehaviour
{
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int VignetteIntensityPropertyId = Shader.PropertyToID("_VignetteIntensity");
    private const float VignetteIntensityStartAmount = 0.0f;
    private const float FeatureToggleEpsilon = 0.001f;

    [Header("References")]
    [SerializeField] private ScriptableRendererFeature _FullScreenBlizzard;
    [SerializeField] private Material _PortalVignetteMaterial;

    [Header("Settings")]
    [SerializeField] private float _vignetteIntensityMaxAmount = 20.0f;
    [SerializeField] private float _smoothSpeed = 50.0f;

    [Header("Tags")]
    [SerializeField] private string _portalTag = "PortalNext";
    [SerializeField] private string _playerTag = "Player";

    [Header("Placement")]
    [SerializeField] private bool _scriptIsOnPortal = false;

    [Header("Debug")]
    [SerializeField] private bool _logDebug = true;

    private float _currentIntensity = VignetteIntensityStartAmount;
    private float _targetIntensity = VignetteIntensityStartAmount;
    private bool _featureEnabled = false;
    private bool _hasPendingColor = false;
    private Color _pendingColor = Color.white;

    public void SetVignetteColor(Color color)
    {
        _pendingColor = color;
        _hasPendingColor = true;

        if (_featureEnabled || _targetIntensity > FeatureToggleEpsilon)
            ApplyPendingVignetteColor();
    }

    void Start()
    {
        if (_FullScreenBlizzard != null)
            _FullScreenBlizzard.SetActive(false);

        if (_PortalVignetteMaterial != null)
            _PortalVignetteMaterial.SetFloat(VignetteIntensityPropertyId, VignetteIntensityStartAmount);

        Log($"Start. OnPortal={_scriptIsOnPortal}, portalTag='{_portalTag}', playerTag='{_playerTag}'. Mat={(_PortalVignetteMaterial ? _PortalVignetteMaterial.name : "NULL")}, Feature={(_FullScreenBlizzard ? _FullScreenBlizzard.name : "NULL")}");

        if (_PortalVignetteMaterial == null) LogWarning("Missing _PortalVignetteMaterial reference.");
        if (_FullScreenBlizzard == null) LogWarning("Missing _FullScreenBlizzard reference.");

        Collider col = GetComponent<Collider>();
        if (col == null) LogWarning("Missing Collider on portal vignette object.");
        else if (!col.isTrigger) LogWarning("Collider should have isTrigger enabled.");

        if (GetComponent<Rigidbody>() == null)
            Log("No Rigidbody found here. Make sure at least one colliding object has one.");
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
        _PortalVignetteMaterial.SetFloat(VignetteIntensityPropertyId, _currentIntensity);

        if (!_featureEnabled && _currentIntensity > FeatureToggleEpsilon)
        {
            _FullScreenBlizzard.SetActive(true);
            _featureEnabled = true;
            Log("Enabled vignette feature.");
        }
        else if (_featureEnabled && _currentIntensity <= FeatureToggleEpsilon)
        {
            _FullScreenBlizzard.SetActive(false);
            _featureEnabled = false;
            Log("Disabled vignette feature.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        string expectedTag = _scriptIsOnPortal ? _playerTag : _portalTag;
        if (other.CompareTag(expectedTag))
        {
            ApplyPendingVignetteColor();
            _targetIntensity = _vignetteIntensityMaxAmount;
            Log($"OnTriggerEnter: '{other.name}' matched '{expectedTag}'.");
        }
        else
        {
            Log($"OnTriggerEnter: ignored '{other.name}' with tag '{other.tag}'. Expected '{expectedTag}'.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        string expectedTag = _scriptIsOnPortal ? _playerTag : _portalTag;
        if (other.CompareTag(expectedTag))
        {
            Log($"OnTriggerExit: '{other.name}' matched '{expectedTag}'.");
            ExitPortal();
        }
    }

    private void ExitPortal()
    {
        _targetIntensity = VignetteIntensityStartAmount;
    }

    private void ApplyPendingVignetteColor()
    {
        if (!_hasPendingColor)
            return;

        if (_PortalVignetteMaterial == null)
        {
            LogWarning("Cannot set vignette color because material is missing.");
            return;
        }

        if (!_PortalVignetteMaterial.HasProperty(ColorPropertyId))
        {
            LogWarning($"Material '{_PortalVignetteMaterial.name}' does not expose _Color.");
            return;
        }

        _PortalVignetteMaterial.SetColor(ColorPropertyId, _pendingColor);
        Log($"Applied vignette color {_pendingColor}.");
    }

    private void Log(string msg)
    {
        if (_logDebug) Debug.Log($"[PortalVignette] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (_logDebug) Debug.LogWarning($"[PortalVignette] {msg}", this);
    }

    private void OnDisable()
    {
        if (_FullScreenBlizzard != null)
        {
            _FullScreenBlizzard.SetActive(false);
            _featureEnabled = false;
            if (_PortalVignetteMaterial != null)
                _PortalVignetteMaterial.SetFloat(VignetteIntensityPropertyId, VignetteIntensityStartAmount);
            Log("OnDisable: turned off vignette feature.");
        }
    }

    private void OnApplicationQuit()
    {
        if (_FullScreenBlizzard != null)
        {
            _FullScreenBlizzard.SetActive(false);
            _featureEnabled = false;
            Log("OnApplicationQuit: turned off vignette feature.");
        }
    }
}
