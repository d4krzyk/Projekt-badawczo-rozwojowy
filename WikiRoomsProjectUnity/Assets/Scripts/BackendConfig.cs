using UnityEngine;

[CreateAssetMenu(fileName = "BackendConfig", menuName = "Configs/BackendConfig")]
public class BackendConfig : ScriptableObject
{
    // Legacy config kept only for scene compatibility after moving backend logic into Unity.
    public string baseURL;
    public string username;
    public string password;
}
