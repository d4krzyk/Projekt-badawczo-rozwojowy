using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

public class PortalVignetteController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScriptableRendererFeature _FullScreenBlizzard;
    [SerializeField] private Material _PortalVignetteMaterial;
    [Header("Settings")]
    [SerializeField] private float _vignetteIntensityMaxAmount = 7.0f;
    [SerializeField] private string _portalTag = "Portal";
    [SerializeField] private LayerMask _portalLayerMask = 0;
    [SerializeField] private float _detectionRadius = 0.5f;
    [SerializeField] private float _smoothSpeed = 3.0f;

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
    }

    // Update is called once per frame
    void Update()
    {
        DetectPortalCollision();
        SmoothVignette();
    }

    private void DetectPortalCollision()
    {
        bool collided = false;

        // jeśli w inspektorze maska = 0 (Nothing), traktujemy to jako "wszystkie warstwy"
        int layerMask = (_portalLayerMask.value == 0) ? ~0 : _portalLayerMask.value;

        // uwzględnij trigger'y (jeśli portal ma isTrigger)
        Collider[] hits = Physics.OverlapSphere(transform.position, _detectionRadius, layerMask, QueryTriggerInteraction.Collide);
        if (hits != null)
        {
            float r2 = _detectionRadius * _detectionRadius;
            foreach (var c in hits)
            {
                if (c == null) continue;
                // pomiń własny collider/obiekt jeśli skrypt jest na tym samym obiekcie
                if (c.gameObject == this.gameObject) continue;
                // porównuj tag i upewnij się, że faktycznie w odległości (dla bezpieczeństwa)
                if (c.CompareTag(_portalTag))
                {
                    if ((c.transform.position - transform.position).sqrMagnitude <= r2 + 0.0001f)
                    {
                        collided = true;
                        break;
                    }
                }
            }
        }

        _targetIntensity = collided ? _vignetteIntensityMaxAmount : VIGNETTE_INTENSITY_START_AMOUNT;
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
        }
        else if (_featureEnabled && _currentIntensity <= epsilon)
        {
            _FullScreenBlizzard.SetActive(false);
            _featureEnabled = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
    }
}
