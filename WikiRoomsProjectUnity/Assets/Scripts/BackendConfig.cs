using UnityEngine;

[CreateAssetMenu(fileName = "BackendConfig", menuName = "Configs/BackendConfig")]
public class BackendConfig : ScriptableObject
{
    public string baseURL;
    public string username;
    public string password;
}
