using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SignController : MonoBehaviour
{
    public static readonly List<SignController> ActiveSigns = new List<SignController>();

    [Header("Render on top")]
    public bool forceMeshOnTop = true;
    public int renderQueueOnTop = 4000;
    public bool forceCanvasOnTop = true;
    public int canvasSortingOrderOnTop = 500;

    private Renderer[] cachedRenderers;
    private Material[][] originalRendererMaterials;
    private Material[][] onTopRendererMaterials;

    private TMP_Text[] cachedTexts;
    private Material[] originalFontMaterials;
    private Material[] onTopFontMaterials;

    private Canvas[] cachedCanvases;
    private bool[] originalCanvasOverride;
    private int[] originalCanvasSorting;

    private bool isOnTop;

    private void OnEnable()
    {
        if (!ActiveSigns.Contains(this))
            ActiveSigns.Add(this);
    }

    private void OnDisable()
    {
        ActiveSigns.Remove(this);
    }

    private void Awake()
    {
        CacheRenderers();
        CacheTextMeshes();
        CacheCanvases();
    }

    public void SetSignText(string content)
    {
        // szukamy dowolnego komponentu tekstowego z TextMeshPro (TextMeshPro lub TextMeshProUGUI)
        TMPro.TMP_Text textComponent = GetComponentInChildren<TMPro.TMP_Text>();
        if (textComponent == null)
        {
            Debug.LogWarning($"SignController: brak komponentu TMP_Text w obiekcie '{gameObject.name}'. Upewnij się, że prefab Sign zawiera TextMeshPro / TextMeshProUGUI.");
            return;
        }

        textComponent.text = content;

        // bezpieczne ustawienie pozycji (jeśli potrzeba korekty)
        Vector3 lp = textComponent.transform.localPosition;
        textComponent.transform.localPosition = new Vector3(lp.x, 0.02f, lp.z);
    }

    public void SetForceOnTop(bool enabled)
    {
        if (enabled == isOnTop) return;
        isOnTop = enabled;

        if (enabled)
        {
            ApplyOnTopMaterials();
            ApplyCanvasOnTop(true);
            ApplyTextOverlay(true);
        }
        else
        {
            RestoreMaterials();
            ApplyCanvasOnTop(false);
            ApplyTextOverlay(false);
        }
    }

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        originalRendererMaterials = new Material[cachedRenderers.Length][];
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            originalRendererMaterials[i] = cachedRenderers[i].sharedMaterials;
        }
    }

    private void CacheTextMeshes()
    {
        cachedTexts = GetComponentsInChildren<TMP_Text>(true);
        originalFontMaterials = new Material[cachedTexts.Length];
        for (int i = 0; i < cachedTexts.Length; i++)
        {
            originalFontMaterials[i] = cachedTexts[i] != null ? cachedTexts[i].fontSharedMaterial : null;
        }
    }

    private void CacheCanvases()
    {
        cachedCanvases = GetComponentsInChildren<Canvas>(true);
        originalCanvasOverride = new bool[cachedCanvases.Length];
        originalCanvasSorting = new int[cachedCanvases.Length];
        for (int i = 0; i < cachedCanvases.Length; i++)
        {
            var canvas = cachedCanvases[i];
            if (canvas == null) continue;
            originalCanvasOverride[i] = canvas.overrideSorting;
            originalCanvasSorting[i] = canvas.sortingOrder;
        }
    }

    private void ApplyOnTopMaterials()
    {
        if (cachedRenderers == null) return;
        if (onTopRendererMaterials == null || onTopRendererMaterials.Length != cachedRenderers.Length)
            onTopRendererMaterials = new Material[cachedRenderers.Length][];

        Shader alwaysOnTopShader = Shader.Find("Unlit/Texture Always On Top");

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            var renderer = cachedRenderers[i];
            if (renderer == null) continue;
            var originals = originalRendererMaterials[i];
            if (originals == null) continue;

            var mats = new Material[originals.Length];
            for (int m = 0; m < originals.Length; m++)
            {
                var src = originals[m];
                if (src == null)
                {
                    mats[m] = null;
                    continue;
                }

                var inst = new Material(src);

                if (forceMeshOnTop && alwaysOnTopShader != null && src.shader != null && src.shader.name == "Unlit/Texture")
                    inst.shader = alwaysOnTopShader;

                if (inst.renderQueue < renderQueueOnTop)
                    inst.renderQueue = renderQueueOnTop;
                if (inst.HasProperty("_ZTest"))
                    inst.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                if (inst.HasProperty("_ZWrite"))
                    inst.SetInt("_ZWrite", 0);

                mats[m] = inst;
            }

            onTopRendererMaterials[i] = mats;
            renderer.sharedMaterials = mats;
        }
    }

    private void RestoreMaterials()
    {
        if (cachedRenderers == null || originalRendererMaterials == null) return;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            var renderer = cachedRenderers[i];
            if (renderer == null) continue;
            renderer.sharedMaterials = originalRendererMaterials[i];
        }
    }

    private void ApplyTextOverlay(bool enabled)
    {
        if (cachedTexts == null) return;

        if (enabled)
        {
            Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");
            if (overlayShader == null)
                overlayShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");

            if (overlayShader == null) return;

            if (onTopFontMaterials == null || onTopFontMaterials.Length != cachedTexts.Length)
                onTopFontMaterials = new Material[cachedTexts.Length];

            for (int i = 0; i < cachedTexts.Length; i++)
            {
                var t = cachedTexts[i];
                if (t == null || originalFontMaterials[i] == null) continue;
                if (onTopFontMaterials[i] == null)
                {
                    onTopFontMaterials[i] = new Material(originalFontMaterials[i]);
                    onTopFontMaterials[i].shader = overlayShader;
                }
                t.fontSharedMaterial = onTopFontMaterials[i];
            }
        }
        else
        {
            for (int i = 0; i < cachedTexts.Length; i++)
            {
                var t = cachedTexts[i];
                if (t == null) continue;
                t.fontSharedMaterial = originalFontMaterials[i];
            }
        }
    }

    private void ApplyCanvasOnTop(bool enabled)
    {
        if (!forceCanvasOnTop || cachedCanvases == null) return;

        for (int i = 0; i < cachedCanvases.Length; i++)
        {
            var canvas = cachedCanvases[i];
            if (canvas == null) continue;

            if (enabled)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = canvasSortingOrderOnTop;
            }
            else
            {
                canvas.overrideSorting = originalCanvasOverride[i];
                canvas.sortingOrder = originalCanvasSorting[i];
            }
        }
    }
}
