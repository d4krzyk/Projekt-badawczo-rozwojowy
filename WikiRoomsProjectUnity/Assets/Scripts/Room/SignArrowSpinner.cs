using UnityEngine;

public class SignArrowSpinner : MonoBehaviour
{
    public float spinSpeed = 60f;
    public Vector3 spinAxis = Vector3.up;

    private void Update()
    {
        if (spinSpeed == 0f) return;
        transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.Self);
    }
}
