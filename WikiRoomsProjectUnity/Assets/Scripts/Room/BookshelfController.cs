using System.Collections.Generic;
using UnityEngine;

public class BookshelfController : MonoBehaviour
{
    public List<GameObject> BookVariants; // różne prefaby book_combination
    public GameObject Sign;
    public Transform[] shelfRowInitialPosition;

    [Header("Sign link arrow")]
    public bool drawSignArrow = true;
    public float signArrowRadius = 0.08f;
    public float signArrowHeadRadius = 0.18f;
    public float signArrowHeadLength = 0.24f;
    public int signArrowConeSegments = 4;
    public float signArrowSpinSpeed = 60f;
    public Vector3 signArrowStartLocalOffsetFromShelf = new Vector3(0f, 2.3f, 0f);
    public Vector3 shelfLinkLocalOffset = new Vector3(0f, 1.9f, 0f);

    [Header("Bookshelf settings")]
    public int booksPerRow = 2;
    public float zStep = -0.63f;   // domyślny przesunięcie w osi Z między książkami (używane gdy brak rendererów)
    public float rowYOffset = 0.517f; // (nieużywane jeśli rowHeights ustawione)
    public float extraGap = 0.08f; // domyślna dodatkowa luka między książkami (jeśli brak specjalnej reguły)

    // wysokości dla kolejnych półek (rzędów): 0 -> pierwsza, 1 -> druga, 2 -> trzecia, 3 -> czwarta
    public float[] rowHeights = new float[] { 1.05f, 0.53f, 0.02f, -0.45f };

    // przesunięcie w osi X gdy "wychodzimy" poza ostatnią półkę (kolejne rzędy zamiast schodzenia w dół)
    public float lastRowShiftX = 0.35f;

    Vector3 startPosition = new Vector3(0,1.05f,0.39f);

    private int bookCount = 0;

    public bool logLocalPositions = true;

    // Stan pomocniczy dla budowania rzędów z różnych szerokości
    private float currentRowLastCenterZ = 0f;
    private float currentRowPrevHalf = 0f;
    private int currentRowIndex = 0;

    // Dodatkowe stany: typ poprzedniego prefabu w bieżącym wierszu (0 = brak/nieznany, 1..4 zgodnie z nazwą)
    private int lastPrefabTypeInRow = 0;
    // jeśli ustawione, ogranicza wybór następnego prefabu w tym wierszu (np. po 3/4 dozwolone tylko 1/2)
    private List<int> allowedNextTypesInRow = null;

    // pomocnik: rozpoznaje typ prefabu wg nazwy (szuka "book_combination", "book_combination2", "book_combination3", "book_combination4")
    private int GetPrefabTypeFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        string n = name.ToLower();
        if (n.Contains("book_combination4")) return 4;
        if (n.Contains("book_combination3")) return 3;
        if (n.Contains("book_combination2")) return 2;
        if (n.Contains("book_combination")) return 1;
        return 0;
    }

    // wybiera prefab losowo, ale respektuje allowedNextTypesInRow jeśli jest ustawione
    private GameObject ChoosePrefabRespectingRules(int index)
    {
        if (BookVariants == null || BookVariants.Count == 0) return null;

        List<GameObject> candidates = new List<GameObject>();

        if (allowedNextTypesInRow == null || allowedNextTypesInRow.Count == 0)
        {
            // wszystkie nie-nullowe prefaby jako kandydaci
            for (int i = 0; i < BookVariants.Count; i++)
            {
                if (BookVariants[i] != null) candidates.Add(BookVariants[i]);
            }
        }
        else
        {
            // filtruj po dozwolonych typach
            for (int i = 0; i < BookVariants.Count; i++)
            {
                var p = BookVariants[i];
                if (p == null) continue;
                int t = GetPrefabTypeFromName(p.name);
                if (allowedNextTypesInRow.Contains(t)) candidates.Add(p);
            }
            // fallback: jeśli nic nie pasuje, użyj wszystkich
            if (candidates.Count == 0)
            {
                for (int i = 0; i < BookVariants.Count; i++)
                {
                    if (BookVariants[i] != null) candidates.Add(BookVariants[i]);
                }
            }
        }

        if (candidates.Count == 0) return null;

        int pick = UnityEngine.Random.Range(0, candidates.Count);
        return candidates[pick];
    }

    public void AddBook(string name, string content, string articleLink, Transform parent, int index)
    {
        if (BookVariants == null || BookVariants.Count == 0)
        {
            Debug.LogWarning("BookshelfController: brak prefabów BookVariants");
            return;
        }

        int col = bookCount % booksPerRow;
        int intendedRow = bookCount / booksPerRow;

        // obliczemy "wizualny" row do użycia dla Y (jeśli wychodzimy poza ostatnią półkę,
        // używamy ostatniego indeksu rowHeights, a kolejne grupy będą przesuwane w lewo)
        int lastRowIndex = (rowHeights != null && rowHeights.Length > 0) ? rowHeights.Length - 1 : 0;
        int visualRow = Mathf.Min(intendedRow, lastRowIndex);
        int overflowRows = Mathf.Max(0, intendedRow - lastRowIndex);

        // oblicz przesunięcie X jeśli jesteśmy "poza" ostatnią półką
        float xPos = startPosition.x - overflowRows * lastRowShiftX;

        // nowy wiersz -> reset stanów dotyczących reguł (rozpoznajemy nowy wizualny wiersz wtedy gdy col==0)
        if (col == 0)
        {
            currentRowIndex = visualRow;
            currentRowLastCenterZ = startPosition.z;
            currentRowPrevHalf = 0f;
            lastPrefabTypeInRow = 0;
            allowedNextTypesInRow = null;
        }

        // wybierz prefab (uwzględniając ewentualne ograniczenia allowedNextTypesInRow)
        GameObject prefab = ChoosePrefabRespectingRules(index);
        if (prefab == null)
        {
            Debug.LogWarning("BookshelfController: wybrany prefab BookVariants jest null");
            return;
        }

        // jeśli prefab jest typu 3 lub 4, ustaw ograniczenie dla kolejnego prefabu w tym wierszu (tylko 1 lub 2)
        int prefabType = GetPrefabTypeFromName(prefab.name);

        // instancjuj prefab jako dziecko parent
        GameObject bookObj = Instantiate(prefab, parent);
        bookObj.name = "Books_" + index;
        bookObj.transform.localRotation = Quaternion.identity;
        bookObj.transform.localScale = Vector3.one;

        // zmierz szerokość (głębokość) obiektu w osi Z. Najpierw spróbuj Renderer, jeśli brak -> użyj zStep
        float sizeZLocal = Mathf.Abs(zStep);
        var rend = bookObj.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            float sizeZWorld = rend.bounds.size.z;
            float parentScaleZ = parent != null ? parent.lossyScale.z : 1f;
            if (parentScaleZ == 0f) parentScaleZ = 1f;
            sizeZLocal = sizeZWorld / parentScaleZ;
        }

        float currHalf = sizeZLocal / 2f;
        float zLocal;

        if (col == 0)
        {
            // jeśli pierwsza książka w wierszu jest typu 1 lub 2, użyj przesunięcia startowego Z = 0.39, w przeciwnym razie użyj domyślnego
            float initialZ = startPosition.z;
            if (prefabType == 1 || prefabType == 2) initialZ = 0.39f;

            currentRowLastCenterZ = initialZ;
            zLocal = currentRowLastCenterZ;
            currentRowPrevHalf = currHalf;

            // ustaw typ dla pierwszego elementu w wierszu
            lastPrefabTypeInRow = prefabType;
            // jeśli ten pierwszy element jest 3/4, to ograniczamy następny
            if (prefabType == 3 || prefabType == 4)
            {
                allowedNextTypesInRow = new List<int>() { 1, 2 };
            }
            else
            {
                allowedNextTypesInRow = null;
            }
        }
        else
        {
            // reguły spacingu:
            // - jeśli poprzedni albo aktualny jest typu 3/4 -> centerDistance = 0.74
            // - jeśli poprzedni i aktualny to typ 1 lub 2 -> centerDistance = 0.36
            // - jeśli poprzedni to 1/2 a aktualny to 3/4 -> centerDistance = 0.48 (specjalna reguła)
            // - w pozostałych przypadkach stosujemy standardowe minimalne odstępy: prevHalf + currHalf + extraGap

            float centerDistance;

            bool prevIs3or4 = (lastPrefabTypeInRow == 3 || lastPrefabTypeInRow == 4);
            bool currIs3or4 = (prefabType == 3 || prefabType == 4);

            bool prevIs1or2 = (lastPrefabTypeInRow == 1 || lastPrefabTypeInRow == 2);
            bool currIs1or2 = (prefabType == 1 || prefabType == 2);

            // najpierw reguła: prev=1/2 & curr=3/4 -> 0.48
            if (prevIs1or2 && currIs3or4)
            {
                centerDistance = 0.48f;
            }
            else if (prevIs3or4 || currIs3or4)
            {
                centerDistance = 0.74f;
            }
            else if (prevIs1or2 && currIs1or2)
            {
                centerDistance = 0.66f;
            }
            else
            {
                centerDistance = currentRowPrevHalf + currHalf + extraGap;
            }

            zLocal = currentRowLastCenterZ - centerDistance;
            currentRowLastCenterZ = zLocal;
            currentRowPrevHalf = currHalf;

            // aktualizuj regułę allowedNextTypesInRow: jeśli bieżący jest 3/4, następny tylko 1/2
            if (prefabType == 3 || prefabType == 4)
            {
                allowedNextTypesInRow = new List<int>() { 1, 2 };
            }
            else
            {
                allowedNextTypesInRow = null;
            }

            // zapamiętaj typ jako ostatni w wierszu
            lastPrefabTypeInRow = prefabType;
        }

        // wybierz wysokość (Y) dla danego wiersza; jeśli poza zakresem -> użyj ostatniej wartości
        float yPos;
        if (rowHeights != null && rowHeights.Length > 0)
        {
            int idx = Mathf.Clamp(visualRow, 0, rowHeights.Length - 1);
            yPos = rowHeights[idx];
        }
        else
        {
            yPos = startPosition.y - visualRow * rowYOffset;
        }

        Vector3 localPos = new Vector3(
            xPos,
            yPos,
            zLocal
        );

        bookObj.transform.localPosition = localPos;

        var bi = bookObj.GetComponent<BookInteraction>();
        if (bi != null)
        {
            bi.bookName = name;
            bi.content = content != null ? content : ("# " + name + "\n");
            bi.bookArticleLink = articleLink + $"#{(name ?? "").Replace(" ", "_")}";
        }

        bookCount++;
    }

    public void AddSign(string name, Transform parent)
    {
        if (Sign == null)
        {
            Debug.LogWarning("BookshelfController: brak prefab Sign");
            return;
        }

        GameObject signObj = Instantiate(Sign);
        signObj.transform.SetParent(parent, false);
        signObj.transform.localPosition = new Vector3(0f, 2.6f, 0f);
        signObj.transform.localRotation = Quaternion.Euler(0f, -270f, 90f);
        signObj.transform.localScale = Vector3.one;

        var sc = signObj.GetComponent<SignController>();
        if (sc != null) sc.SetSignText(name);


        if (drawSignArrow)
        {
            CreateSignArrow(signObj, parent);
        }
    }

    private static Mesh cachedConeMesh;
    private Material cachedArrowMaterial;

    private void CreateSignArrow(GameObject signObj, Transform parent)
    {
        if (signObj == null || parent == null) return;

        Vector3 startWorld = parent.TransformPoint(signArrowStartLocalOffsetFromShelf);
        Vector3 endWorld = parent.TransformPoint(shelfLinkLocalOffset);
        float height = endWorld.y - startWorld.y;
        float totalLength = Mathf.Abs(height);
        if (totalLength <= 0.001f) return;

        Vector3 dir = (height >= 0f) ? Vector3.up : Vector3.down;
        float headLength = Mathf.Clamp(signArrowHeadLength, 0.01f, totalLength * 0.7f);
        float shaftLength = Mathf.Max(0.01f, totalLength - headLength);

        GameObject arrowRoot = new GameObject("SignArrow");
        arrowRoot.transform.SetParent(parent, true);
        arrowRoot.transform.position = startWorld + dir * (totalLength * 0.5f);
        arrowRoot.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);

        GameObject arrowSpinRoot = new GameObject("ArrowSpinRoot");
        arrowSpinRoot.transform.SetParent(arrowRoot.transform, false);

        var spinner = arrowSpinRoot.AddComponent<SignArrowSpinner>();
        spinner.spinSpeed = signArrowSpinSpeed;

        GameObject shaft = CreateArrowPartCylinder(arrowSpinRoot.transform, "Shaft");
        shaft.transform.localPosition = new Vector3(0f, -totalLength * 0.5f + shaftLength * 0.5f, 0f);
        shaft.transform.localRotation = Quaternion.identity;
        shaft.transform.localScale = new Vector3(signArrowRadius, shaftLength, signArrowRadius);

        GameObject head = CreateArrowPartCone(arrowSpinRoot.transform, "Head");
        head.transform.localPosition = new Vector3(0f, totalLength * 0.5f - headLength, 0f);
        head.transform.localRotation = Quaternion.identity;
        head.transform.localScale = new Vector3(signArrowHeadRadius, headLength, signArrowHeadRadius);
    }

    private GameObject CreateArrowPartCylinder(Transform parent, string name)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        var col = part.GetComponent<Collider>();
        if (col != null) Destroy(col);

        ApplyArrowMaterial(part.GetComponent<MeshRenderer>());
        return part;
    }

    private GameObject CreateArrowPartCone(Transform parent, string name)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        var mf = part.AddComponent<MeshFilter>();
        var mr = part.AddComponent<MeshRenderer>();
        mf.sharedMesh = GetConeMesh(signArrowConeSegments);
        ApplyArrowMaterial(mr);
        return part;
    }

    private void ApplyArrowMaterial(MeshRenderer renderer)
    {
        if (renderer == null) return;
        renderer.sharedMaterial = GetArrowMaterial();
    }

    private Material GetArrowMaterial()
    {
        if (cachedArrowMaterial == null)
        {
            cachedArrowMaterial = new Material(Shader.Find("Unlit/Texture"));
            cachedArrowMaterial.color = Color.white;
        }
        return cachedArrowMaterial;
    }

    private Mesh GetConeMesh(int segments)
    {
        if (cachedConeMesh != null) return cachedConeMesh;

        int seg = Mathf.Max(3, segments);
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        int tipIndex = 0;
        vertices.Add(new Vector3(0f, 1f, 0f));

        for (int i = 0; i < seg; i++)
        {
            float angle = (Mathf.PI * 2f * i) / seg;
            vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
        }

        int baseCenterIndex = vertices.Count;
        vertices.Add(Vector3.zero);

        for (int i = 0; i < seg; i++)
        {
            int curr = 1 + i;
            int next = 1 + ((i + 1) % seg);
            triangles.Add(tipIndex);
            triangles.Add(next);
            triangles.Add(curr);
        }

        for (int i = 0; i < seg; i++)
        {
            int curr = 1 + i;
            int next = 1 + ((i + 1) % seg);
            triangles.Add(baseCenterIndex);
            triangles.Add(curr);
            triangles.Add(next);
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        cachedConeMesh = mesh;
        return cachedConeMesh;
    }

    public void AddBookElongatedRoom(string name, string content, string articleLink, Transform parent)
    {
        
    }
    

    public void ResetLayout()
    {
        bookCount = 0;
        currentRowLastCenterZ = 0f;
        currentRowPrevHalf = 0f;
        currentRowIndex = 0;
        lastPrefabTypeInRow = 0;
        allowedNextTypesInRow = null;
    }
}
