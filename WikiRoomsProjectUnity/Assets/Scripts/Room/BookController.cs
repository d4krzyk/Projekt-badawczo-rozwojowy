using UnityEngine;
using TMPro;

public class BookController : MonoBehaviour
{
    public TMP_Text leftPage;
    public TMP_Text rightPage;

    public void PreviousPage()
    {
        if (leftPage.pageToDisplay < 1)
        {
            leftPage.pageToDisplay = 1;
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
    }

    public void NextPage()
    {
        if (rightPage.pageToDisplay >= rightPage.textInfo.pageCount)
        {
            return;
        }

        rightPage.pageToDisplay += 2;
        leftPage.pageToDisplay += 2;
    }

    public void ResetPages()
    {
        leftPage.pageToDisplay = 1;
        rightPage.pageToDisplay = 2;
    }
}
