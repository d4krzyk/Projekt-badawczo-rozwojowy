using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LoadingPuzzleMotion : MonoBehaviour
{
    public enum MotionMode { Rotate, Wave }

    [System.Serializable]
    public class PuzzleItem
    {
        public RectTransform rect;
        [HideInInspector] public bool started; // kontrola startu ruchu
        [HideInInspector] public bool reachedEnd; // czy doszedł do końca
        [HideInInspector] public float baseY; // zapamiętana pozycja Y
        [HideInInspector] public float phase; // faza dla fali
    }

    public PuzzleItem[] puzzles;

    // wspólny speed dla wszystkich puzzli
    public float speed = 100f;

    public float startX = -300f;
    public float endX = 2200f;

    [Header("Motion")]
    public MotionMode motionMode = MotionMode.Rotate;
    public bool applyOnlyWhenStarted = true; // dotyczy obu efektów: rotacji i fali

    [Header("Rotation (pixel-snappy)")]
    public float rotationInterval = 0.5f; // sekundy
    public float rotationStep = 90f; // stopnie na skok

    [Header("Wave (sinus)")]
    public float waveAmplitude = 10f;
    public float waveFrequency = 1f; // Hz
    public float phaseOffset = 0.5f; // faza między kolejnymi puzzlami

    private Coroutine _rotationCoroutine;

    void Start()
    {
        for (int i = 0; i < puzzles.Length; i++)
        {
            var p = puzzles[i];
            if (p == null || p.rect == null) continue;
            Vector2 pos = p.rect.anchoredPosition;
            pos.x = startX;
            p.rect.anchoredPosition = pos;
            p.started = false;
            p.reachedEnd = false;
            // zapamiętaj bazową pozycję Y i ustaw fazę
            p.baseY = p.rect.anchoredPosition.y;
            p.phase = i * phaseOffset;
            // upewnij się, że początkowy obrót jest wielokrotnością rotationStep (opcjonalne)
            var e = p.rect.localEulerAngles;
            e.z = Mathf.Round(e.z / rotationStep) * rotationStep;
            p.rect.localEulerAngles = e;
        }

        StartCoroutine(RunRandomSequence());

        if (motionMode == MotionMode.Rotate && rotationInterval > 0f)
            _rotationCoroutine = StartCoroutine(RotateSnappy());
    }

    void Update()
    {
        foreach (var p in puzzles)
        {
            if (!p.started) continue;

            p.rect.anchoredPosition += Vector2.right * speed * Time.deltaTime;

            if (p.rect.anchoredPosition.x > endX)
            {
                p.reachedEnd = true;
                Vector2 pos = p.rect.anchoredPosition;
                pos.x = startX;
                // przy restarcie pozycji x zachowaj baseY (nie nadpisuj)
                pos.y = p.baseY;
                p.rect.anchoredPosition = pos;
            }
        }

        // Wave effect - aktualizujemy Y natychmiastowo (skokowo nie jest potrzebny tutaj)
        if (motionMode == MotionMode.Wave)
        {
            float t = Time.time;
            foreach (var p in puzzles)
            {
                if (p == null || p.rect == null) continue;
                if (applyOnlyWhenStarted && !p.started) { 
                    // przywróć bazową pozycję jeśli nie started
                    var posIdle = p.rect.anchoredPosition;
                    posIdle.y = p.baseY;
                    p.rect.anchoredPosition = posIdle;
                    continue;
                }
                float y = p.baseY + Mathf.Sin(t * Mathf.PI * 2f * waveFrequency + p.phase) * waveAmplitude;
                var pos = p.rect.anchoredPosition;
                pos.y = y;
                p.rect.anchoredPosition = pos;
            }
        }
    }

    private IEnumerator RunRandomSequence()
    {
        int lastIndex = -1;

        if (puzzles == null || puzzles.Length == 0)
            yield break;

        while (true)
        {
            // wybierz losowy inny niż poprzedni
            int idx;
            if (puzzles.Length == 1)
                idx = 0;
            else
            {
                do { idx = Random.Range(0, puzzles.Length); }
                while (idx == lastIndex);
            }

            var p = puzzles[idx];
            if (p == null || p.rect == null)
            {
                lastIndex = idx;
                yield return null;
                continue;
            }

            // uruchom tylko tego puzzla (bez delay)
            p.started = true;

            // poczekaj aż dojdzie do końca
            while (!p.reachedEnd)
                yield return null;

            // zresetuj i wyłącz jego started, zapisz jako ostatni
            p.reachedEnd = false;
            p.started = false;
            lastIndex = idx;

            // krótka pauza, by nie od razu wrócić w tej samej klatce
            yield return null;
        }
    }

    private IEnumerator RotateSnappy()
    {
        if (rotationInterval <= 0f) yield break;
        var wait = new WaitForSeconds(rotationInterval);

        while (true)
        {
            foreach (var p in puzzles)
            {
                if (p == null || p.rect == null) continue;
                if (applyOnlyWhenStarted && !p.started) continue;

                var e = p.rect.localEulerAngles;
                float nextZ = e.z + rotationStep;
                nextZ = Mathf.Repeat(nextZ, 360f);
                e.z = nextZ;
                p.rect.localEulerAngles = e; // natychmiastowy, "skokowy" obrót
            }

            yield return wait;
        }
    }

    public void StartSequentially(GameObject[] items, float delayBetween)
    {
        StartCoroutine(StaggerStart(items, delayBetween));
    }

    private IEnumerator StaggerStart(GameObject[] items, float delay)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
            {
                items[i].SetActive(true);
            }
            yield return new WaitForSeconds(delay);
        }
    }

    private void OnDisable()
    {
        if (_rotationCoroutine != null) StopCoroutine(_rotationCoroutine);
        StopAllCoroutines();
        if (puzzles == null) return;
        foreach (var p in puzzles)
        {
            if (p == null) continue;
            p.started = false;
            p.reachedEnd = false;
        }
    }

    public void ResetAnimation()
    {
        // Zatrzymaj wszystkie korutyny
        if (_rotationCoroutine != null) StopCoroutine(_rotationCoroutine);
        StopAllCoroutines();

        // Zresetuj stan wszystkich puzzli
        for (int i = 0; i < puzzles.Length; i++)
        {
            var p = puzzles[i];
            if (p == null || p.rect == null) continue;
            Vector2 pos = p.rect.anchoredPosition;
            pos.x = startX;
            p.rect.anchoredPosition = pos;
            p.started = false;
            p.reachedEnd = false;
            p.baseY = p.rect.anchoredPosition.y;
            p.phase = i * phaseOffset;
            var e = p.rect.localEulerAngles;
            e.z = Mathf.Round(e.z / rotationStep) * rotationStep;
            p.rect.localEulerAngles = e;
        }

        // Uruchom korutyny ponownie
        StartCoroutine(RunRandomSequence());
        if (motionMode == MotionMode.Rotate && rotationInterval > 0f)
            _rotationCoroutine = StartCoroutine(RotateSnappy());
    }
}
