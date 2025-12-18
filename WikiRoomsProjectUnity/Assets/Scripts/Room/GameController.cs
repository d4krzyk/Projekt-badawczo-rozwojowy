using UnityEngine;

public class GameController : MonoBehaviour
{
    public string ArticleName;
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
