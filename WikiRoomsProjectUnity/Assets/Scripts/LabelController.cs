using LogicUI.FancyTextRendering;
using UnityEngine;

public class LabelController : MonoBehaviour
{
    public GameObject labelValuePrefab;
    public GameObject labelText;
    public GameObject labelValuesContainer;

    public void SetLabelText(string text)
    {
        labelText.GetComponent<MarkdownRenderer>().Source = text;
    }
    public void AddLabelValue(string text)
    {
        GameObject labelValueObject = Instantiate(labelValuePrefab);
        labelValueObject.transform.SetParent(labelValuesContainer.transform, false);
        labelValueObject.GetComponent<MarkdownRenderer>().Source = text;
    }
}
