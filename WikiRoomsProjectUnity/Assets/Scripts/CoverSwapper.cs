using System.Collections.Generic;
using UnityEngine;

public class CoverSwapper : MonoBehaviour
{
    [Header("Źródła")]
    public List<Material> CoverMaterials;
    public Material PaperMaterial; // materiał papieru -> element 1

    [Header("Ustawienia")]
    public bool instantiateMaterialPerBook = true;

    // prefiks nazwy childa, którego renderer ma być zmieniony (np. "Cube" lub "cube")
    public string coverChildNamePrefix = "cube";

    // który indeks materiału podmieniamy (domyślnie 0) i który indeks użyć dla papieru (domyślnie 1)
    public int coverMaterialIndex = 0;
    public int paperMaterialIndex = 1;

    void Awake()
    {
        // automatycznie podmień materiał zaraz po instancjonowaniu prefab
        RandomizeCover();
    }

    // Publiczne API — wywołaj, aby losowo podmienić materiał okładki tej instancji prefab
    public void RandomizeCover()
    {
        if ((CoverMaterials == null || CoverMaterials.Count == 0) && PaperMaterial == null) return;

        Renderer target = FindTargetRenderer();
        if (target == null) return;

        var mats = target.materials;
        if (mats == null) mats = new Material[0];

        int maxIdx = Mathf.Max(coverMaterialIndex, paperMaterialIndex);

        // jeśli tablica materiałów za krótka, rozszerz ją do co najmniej length = maxIdx+1
        if (mats.Length <= maxIdx)
        {
            var newMats = new Material[maxIdx + 1];
            for (int i = 0; i < newMats.Length; i++)
            {
                newMats[i] = (i < mats.Length) ? mats[i] : null;
            }
            mats = newMats;
        }

        // ustaw materiał okładki na coverMaterialIndex (losowo z listy CoverMaterials, jeśli dostępne)
        if (CoverMaterials != null && CoverMaterials.Count > 0)
        {
            var mat = CoverMaterials[Random.Range(0, CoverMaterials.Count)];
            if (mat != null)
            {
                mats[coverMaterialIndex] = instantiateMaterialPerBook ? new Material(mat) : mat;
            }
        }

        // ustaw materiał papieru na paperMaterialIndex (jeśli podany)
        if (PaperMaterial != null)
        {
            mats[paperMaterialIndex] = instantiateMaterialPerBook ? new Material(PaperMaterial) : PaperMaterial;
        }

        target.materials = mats;
    }

    private Renderer FindTargetRenderer()
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0) return null;

        if (string.IsNullOrEmpty(coverChildNamePrefix))
            return rends[0];

        string pref = coverChildNamePrefix.ToLower();
        // najpierw spróbuj znaleźć renderer którego nazwa zaczyna się od prefiksu
        foreach (var r in rends)
        {
            if (r == null) continue;
            if (r.gameObject.name.ToLower().StartsWith(pref))
                return r;
        }

        // jeśli nie znaleziono, spróbuj zawierania prefiksu
        foreach (var r in rends)
        {
            if (r == null) continue;
            if (r.gameObject.name.ToLower().Contains(pref))
                return r;
        }

        // fallback na pierwszy renderer
        return rends[0];
    }
}
