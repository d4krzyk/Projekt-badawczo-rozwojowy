using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        transform.LookAt(transform.position + cam.transform.forward);
        transform.Rotate(180f, 90f, -90f);
    }
}
