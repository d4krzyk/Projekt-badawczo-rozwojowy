using UnityEngine;

public class GameController : MonoBehaviour
{
    public string ArticleName;
    public string PlayerNick;
    public string TargetArticleName;
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
