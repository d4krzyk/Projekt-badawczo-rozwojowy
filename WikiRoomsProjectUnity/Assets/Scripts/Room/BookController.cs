using UnityEngine;
using TMPro;

public class BookController : MonoBehaviour
{
    public TMP_Text leftPage;
    public TMP_Text rightPage;
    public GameObject previousPageArrow;
    public GameObject nextPageArrow;

    void OnEnable()
    {
        UpdateArrowVisibility();
    }

    public void PreviousPage()
    {
        if (leftPage.pageToDisplay < 1)
        {
            leftPage.pageToDisplay = 1;
            UpdateArrowVisibility();
            return;
        }

        if (leftPage.pageToDisplay - 2 > 1)
        {
            leftPage.pageToDisplay -= 2;
        }
        else
        {
            leftPage.pageToDisplay = 1;
        }

        rightPage.pageToDisplay = leftPage.pageToDisplay + 1;
        UpdateArrowVisibility();
    }

    public void NextPage()
    {
        if (rightPage.pageToDisplay >= rightPage.textInfo.pageCount)
        {
            UpdateArrowVisibility();
            return;
        }

        rightPage.pageToDisplay += 2;
        leftPage.pageToDisplay += 2;
        UpdateArrowVisibility();
    }

    public void ResetPages()
    {
        leftPage.pageToDisplay = 1;
        rightPage.pageToDisplay = 2;
        UpdateArrowVisibility();
    }

    void UpdateArrowVisibility()
    {
        if (leftPage == null || rightPage == null) return;

        leftPage.ForceMeshUpdate();
        rightPage.ForceMeshUpdate();

        bool canGoPrev = leftPage.pageToDisplay > 1;
        int pageCount = rightPage.textInfo != null ? rightPage.textInfo.pageCount : 0;
        bool canGoNext = pageCount > 0 && rightPage.pageToDisplay < pageCount;

        if (previousPageArrow != null) previousPageArrow.SetActive(canGoPrev);
        if (nextPageArrow != null) nextPageArrow.SetActive(canGoNext);
    }
}
