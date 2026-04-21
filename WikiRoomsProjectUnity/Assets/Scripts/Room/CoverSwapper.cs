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

    [Header("Weighted Random")]
    public int rareMaterialsThreshold = 9;    // indeksy 0-9 są rzadkie
    public int commonMaterialsWeight = 10;    // waga dla czestych (od indeksu 10+)
    public int rareMaterialsWeight = 1;       // waga dla rzadkich (0-9)

    void Awake()
    {
        // automatycznie podmień materiał zaraz po instancjonowaniu prefab
        RandomizeCovers();
    }

    // Publiczne API — wywołaj, aby losowo podmienić materiały okładek wszystkich książek
    public void RandomizeCovers()
    {
        if ((CoverMaterials == null || CoverMaterials.Count == 0) && PaperMaterial == null) return;

        Renderer[] targets = FindAllTargetRenderers();
        if (targets == null || targets.Length == 0) return;

        // dla każdego renderera (każdej "książki")
        foreach (Renderer target in targets)
        {
            if (target == null) continue;

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
                int randomIndex = GetWeightedRandomMaterialIndex();
                var mat = CoverMaterials[randomIndex];
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
    }

    private Renderer[] FindAllTargetRenderers()
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0) return null;

        if (string.IsNullOrEmpty(coverChildNamePrefix))
            return rends;

        // zbierz wszystkie renderery które pasują do prefiksu
        var filtered = new List<Renderer>();
        string pref = coverChildNamePrefix.ToLower();
        
        foreach (var r in rends)
        {
            if (r == null) continue;
            if (r.gameObject.name.ToLower().Contains(pref))
                filtered.Add(r);
        }

        // jeśli znaleziono, zwróć filtered, inaczej wszystkie
        return filtered.Count > 0 ? filtered.ToArray() : rends;
    }

    // Losuje indeks materiału z podanym wagowaniem (czeste 10+ vs rzadkie 0-9)
    private int GetWeightedRandomMaterialIndex()
    {
        int rareCount = Mathf.Min(rareMaterialsThreshold + 1, CoverMaterials.Count);
        int commonCount = Mathf.Max(0, CoverMaterials.Count - rareCount);

        // oblicz całkowitą wagę
        int totalWeight = rareCount * rareMaterialsWeight + commonCount * commonMaterialsWeight;
        int randomWeight = Random.Range(0, totalWeight);

        // jeśli wypadnie na przedział rzadkich (0-9)
        if (randomWeight < rareCount * rareMaterialsWeight)
            return Random.Range(0, rareCount);
        else
            return Random.Range(rareCount, CoverMaterials.Count);
    }
}
