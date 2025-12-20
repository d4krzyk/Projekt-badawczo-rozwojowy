using UnityEngine;
using TMPro;
using System.Collections;

public class LoadingTextAnimation : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI targetText;
    [SerializeField] private string baseText = "Loading";
    [SerializeField] private float interval = 1f; // sekundy

    private Coroutine _loop;

    void Start()
    {
        if (targetText == null) targetText = GetComponent<TextMeshProUGUI>();
        if (targetText == null) return;
        _loop = StartCoroutine(LoopDots());
    }

    private IEnumerator LoopDots()
    {
        int step = 0; // 0:"Loading", 1:"Loading.", 2:"Loading..", 3:"Loading..."
        while (true)
        {
            switch (step)
            {
                case 0: targetText.text = baseText; break;
                case 1: targetText.text = baseText + "."; break;
                case 2: targetText.text = baseText + ".."; break;
                default: targetText.text = baseText + "..."; break;
            }

            step = (step + 1) % 4;
            yield return new WaitForSeconds(interval);
        }
    }

    private void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
    }
}
