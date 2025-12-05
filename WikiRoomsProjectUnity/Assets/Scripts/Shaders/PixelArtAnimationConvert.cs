using UnityEngine;

public class PixelArtAnimationConvert : MonoBehaviour
{   public Animation anim;
    public string clipName;
    public float targetFPS = 4f;

    private float frameTime;
    private AnimationState state;

    void Start()
    {
        state = anim[clipName];
        state.speed = 0f;        // STOP normal playback
        anim.Play(clipName);     // start at frame 0
        frameTime = 0f;
    }

    void Update()
    {
        frameTime += Time.deltaTime;

        if (frameTime >= 1f / targetFPS)
        {
            // przesuwamy czas animacji o jedną "pixel-art klatkę"
            state.time += 1f / targetFPS;

            // jeśli dojedziemy do końca — loop
            if (state.time > state.length)
                state.time -= state.length;

            anim.Sample(); // zastosuj
            frameTime = 0f;
        }
    }

}
