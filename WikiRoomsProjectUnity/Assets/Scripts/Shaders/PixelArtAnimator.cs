using UnityEngine;

public class PixelArtAnimator : MonoBehaviour
{
    public Animator animator;
    public float targetFPS = 6f; // ile klatek ma wyglądać (pixel-art vibe)

    private float frameTimer = 0f;

    void Update()
    {
        frameTimer += Time.deltaTime;

        // jeśli minął czas jednej "retro klatki"
        if (frameTimer >= 1f / targetFPS)
        {
            animator.Update(1f / targetFPS);
            frameTimer = 0f;
        }
    }
}
