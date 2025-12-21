using UnityEngine;

public class GameController : MonoBehaviour
{
    public string ArticleName;
    public string PlayerNick;
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
