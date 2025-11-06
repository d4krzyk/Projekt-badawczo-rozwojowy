using UnityEngine;

public class PS1CameraSnap : MonoBehaviour
{
    public float pixelSize = 1f / 480f; // dla 480p
    void LateUpdate()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Round(pos.x / pixelSize) * pixelSize;
        pos.y = Mathf.Round(pos.y / pixelSize) * pixelSize;
        pos.z = Mathf.Round(pos.z / pixelSize) * pixelSize;
        transform.position = pos;
    }
}
