using UnityEngine;

public class PortalLift : MonoBehaviour
{
    public float pullSpeed = 1f;           // jak szybko gracz idzie w górę (metry/s)
    public float rotateSpeed = 360f;       // docelowa prędkość kątowa (deg/s)
    public float liftHeight = 2f;          // ile ma polecieć do góry
    public float angularAcceleration = 360f;// przyspieszenie kątowe (deg/s^2) - zachowany dla kompatybilności

    public Transform portalVFX;

    // parametry szybkiego przemieszczenia do środka (animacja)
    public float centerMoveDuration = 1.5f;   // czas animacji przeniesienia do środka (s)
    public AnimationCurve centerMoveCurve = AnimationCurve.EaseInOut(0,0,1,1);

    // NOWE: krzywa sterująca obrotem w czasie liftu (0..1 -> mnożnik prędkości obrotu)
    public AnimationCurve rotateSpeedCurve = AnimationCurve.EaseInOut(0,0,1,1);

    // OPCJONALNIE: krzywa płynności unoszenia (jeśli false, używane MoveTowards z pullSpeed)
    public bool useLiftYCurve = false;
    public AnimationCurve liftYCurve = AnimationCurve.EaseInOut(0,0,1,1);

    // NOWE: audio / pitch
    public AudioSource portalAudioSource;                 // jeśli null, spróbuje pobrać z portalVFX lub tego obiektu
    public AnimationCurve audioPitchCurve = AnimationCurve.Linear(0, 0, 1, 1); // 0..1 -> mnożnik mapowany do min..max
    public float audioPitchMin = 0.8f;
    public float audioPitchMax = 1.2f;
    public bool affectDuringMoveToCenter = true;         // czy również modyfikować pitch podczas MoveToCenter

    private enum Phase { Idle, MoveToCenter, Lift, Hover }
    private Phase phase = Phase.Idle;

    private Transform player;
    private Rigidbody playerRb;
    private PlayerController playerController;

    private Vector3 portalCenter;
    private Vector3 moveStartPos;
    private float moveElapsed;

    private float startY;
    private float targetY;

    // zapamiętane stany rigidbody
    private bool previousUseGravity;
    private bool previousIsKinematic;
    private float currentAngularSpeed = 0f;

    // timery do liftu
    private float liftElapsed = 0f;
    private float liftDuration = 1f;

    // kamery: zapamiętana lokalna rotacja na starcie snapa
    private Quaternion camStartLocalRot = Quaternion.identity;

    // audio: zapamiętany początkowy pitch, aby przywrócić po zakończeniu
    private float previousAudioPitch = 1f;

    // NOWE: parametry hover (bujanie w fazie Hover)
    public float hoverAmplitude = 0.15f;   // metry
    public float hoverFrequency = 0.8f;    // Hz
    private float hoverTimer = 0f;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && phase == Phase.Idle)
        {
            player = other.transform;
            playerRb = other.GetComponent<Rigidbody>();
            playerController = other.GetComponent<PlayerController>();

            portalCenter = portalVFX != null ? portalVFX.position : transform.position;

            // przygotuj animację przeniesienia do środka (XZ only — Y zostawiaj bez zmian)
            moveStartPos = playerRb != null ? playerRb.position : player.position;
            moveElapsed = 0f;
            phase = Phase.MoveToCenter;

            // zapamiętaj startową rotację kamery (jeśli dostępna)
            if (playerController != null && playerController.cameraTransform != null)
                camStartLocalRot = playerController.cameraTransform.localRotation;
            else
                camStartLocalRot = Quaternion.identity;

            if (playerRb != null)
            {
                // zapamiętaj i wyłącz fizykę — ruch wykonamy manipulując transformem (eliminujemy drgania)
                previousUseGravity = playerRb.useGravity;
                previousIsKinematic = playerRb.isKinematic;

                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
                playerRb.useGravity = false;
                playerRb.isKinematic = true; // wyłączamy wpływ kolizji i fizyki podczas snapa
            }
            else
            {
                previousUseGravity = false;
                previousIsKinematic = false;
            }

            // audio: znajdź źródło jeśli nie przypisano i zapamiętaj oryginalny pitch
            if (portalAudioSource == null)
            {
                if (portalVFX != null) portalAudioSource = portalVFX.GetComponent<AudioSource>();
                if (portalAudioSource == null) portalAudioSource = GetComponent<AudioSource>();
            }
            if (portalAudioSource != null)
            {
                previousAudioPitch = portalAudioSource.pitch;
            }

            // zablokuj kontrolę gracza
            if (playerController != null)
                playerController.movementLocked = true;
        }
    }

    void FixedUpdate()
    {
        if (phase == Phase.Idle || player == null) return;

        float dt = Time.fixedDeltaTime;

        if (phase == Phase.MoveToCenter)
        {
            moveElapsed += dt;
            float t = Mathf.Clamp01(moveElapsed / Mathf.Max(0.0001f, centerMoveDuration));
            float eval = centerMoveCurve.Evaluate(t);

            // interpolacja tylko w XZ — Y pozostaje na wysokości startowej gracza
            Vector2 startXZ = new Vector2(moveStartPos.x, moveStartPos.z);
            Vector2 targetXZ = new Vector2(portalCenter.x, portalCenter.z);
            Vector2 nextXZ = Vector2.Lerp(startXZ, targetXZ, eval);
            float nextY = moveStartPos.y;
            Vector3 next = new Vector3(nextXZ.x, nextY, nextXZ.y);

            // manipulujemy transform bez fizyki (isKinematic = true)
            if (playerRb != null)
                playerRb.transform.position = next;
            else
                player.position = next;

            // płynne prostowanie kamery (lokalna rotacja) równolegle do ruchu
            if (playerController != null && playerController.cameraTransform != null)
            {
                Quaternion targetCamLocal = Quaternion.identity;
                Quaternion s = Quaternion.Slerp(camStartLocalRot, targetCamLocal, eval);
                playerController.cameraTransform.localRotation = s;
            }

            if (t >= 1f)
            {
                // zakończ animację przeniesienia i rozpocznij lift
                phase = Phase.Lift;
                currentAngularSpeed = 0f;

                // ważne: nie ustawiamy startY na wysokości portalCenter — używamy aktualnej wysokości gracza
                float currentY = playerRb != null ? playerRb.transform.position.y : player.position.y;
                startY = currentY;
                targetY = startY + liftHeight;

                // ustaw dokładnie XZ centrum, zachowując Y startową wysokość
                Vector3 fixedPos = new Vector3(portalCenter.x, startY, portalCenter.z);
                if (playerRb != null) playerRb.transform.position = fixedPos; else player.position = fixedPos;

                // initializuj lift timer
                liftElapsed = 0f;
                liftDuration = Mathf.Max(0.0001f, Mathf.Abs(liftHeight) / Mathf.Max(0.0001f, Mathf.Abs(pullSpeed)));
            }

            return;
        }

        if (phase == Phase.Lift)
        {
            // sterowanie rotacją przez krzywą zależną od czasu liftu
            liftElapsed += dt;
            float tLift = Mathf.Clamp01(liftElapsed / Mathf.Max(0.0001f, liftDuration)); // 0..1

            // rotacja: mnożnik z krzywej (0..1 typowo) razy rotateSpeed (deg/s)
            float rotFactor = rotateSpeedCurve.Evaluate(tLift);
            currentAngularSpeed = rotateSpeed * rotFactor;
            float deltaAngle = currentAngularSpeed * dt;
            Quaternion deltaRot = Quaternion.Euler(0f, deltaAngle, 0f);

            // audio podczas liftu
            if (portalAudioSource != null)
            {
                float pitchFactor = audioPitchCurve.Evaluate(tLift);
                portalAudioSource.pitch = Mathf.Lerp(audioPitchMin, audioPitchMax, pitchFactor);
            }

            if (playerRb != null)
            {
                Vector3 curr = playerRb.transform.position;
                Vector3 offset = curr - portalCenter;
                Vector3 offsetXZ = new Vector3(offset.x, 0f, offset.z);
                Vector3 rotatedXZ = deltaRot * offsetXZ;
                Vector3 newHorizontal = portalCenter + rotatedXZ;

                float newY;
                if (useLiftYCurve)
                {
                    float yFactor = liftYCurve.Evaluate(tLift); // 0..1 mapped by curve
                    newY = Mathf.Lerp(startY, targetY, yFactor);
                }
                else
                {
                    newY = Mathf.MoveTowards(curr.y, targetY, pullSpeed * dt);
                }

                Vector3 newPos = new Vector3(newHorizontal.x, newY, newHorizontal.z);

                // ustawiamy transform (isKinematic = true)
                playerRb.transform.position = newPos;
                playerRb.transform.rotation = playerRb.transform.rotation * deltaRot;
            }
            else
            {
                Vector3 curr = player.position;
                Vector3 offset = curr - portalCenter;
                Vector3 offsetXZ = new Vector3(offset.x, 0f, offset.z);
                Vector3 rotatedXZ = deltaRot * offsetXZ;
                Vector3 newHorizontal = portalCenter + rotatedXZ;

                float newY;
                if (useLiftYCurve)
                {
                    float yFactor = liftYCurve.Evaluate(tLift);
                    newY = Mathf.Lerp(startY, targetY, yFactor);
                }
                else
                {
                    newY = Mathf.MoveTowards(curr.y, targetY, pullSpeed * dt);
                }

                Vector3 newPos = new Vector3(newHorizontal.x, newY, newHorizontal.z);

                player.position = newPos;
                player.Rotate(Vector3.up * deltaAngle, Space.World);
            }

            // zakończenie po osiągnięciu wysokości (jeśli używamy krzywej Y, końimy gdy tLift==1)
            float currY = playerRb != null ? playerRb.transform.position.y : player.position.y;
            bool reachedY = useLiftYCurve ? (tLift >= 1f - 0.0001f) : (Mathf.Abs(currY - targetY) < 0.05f);

            if (reachedY)
            {
                // ustaw dokładnie finalną pozycję i przejdź do fazy Hover (ciągła rotacja w stałej prędkości)
                Vector3 finalPos = new Vector3(portalCenter.x, targetY, portalCenter.z);
                if (playerRb != null) playerRb.transform.position = finalPos; else player.position = finalPos;

                phase = Phase.Hover;
                // zapewnij, że currentAngularSpeed ustawia się na ostateczną prędkość
                currentAngularSpeed = rotateSpeed;

                // resetuj timer hover i ustaw docelowy pitch w hover (koniec krzywej)
                hoverTimer = 0f;
                if (portalAudioSource != null)
                    portalAudioSource.pitch = Mathf.Lerp(audioPitchMin, audioPitchMax, audioPitchCurve.Evaluate(1f));
            }
        }

        if (phase == Phase.Hover)
        {
            // bujanie: sinusiczny bob w pionie
            hoverTimer += dt;
            float bob = Mathf.Sin(hoverTimer * Mathf.PI * 2f * hoverFrequency) * hoverAmplitude;

            // obracaj ciągle wokół środka portalCenter przy stałej prędkości rotateSpeed
            float deltaAngle = rotateSpeed * dt;
            Quaternion deltaRot = Quaternion.Euler(0f, deltaAngle, 0f);

            if (playerRb != null)
            {
                Vector3 curr = playerRb.transform.position;
                Vector3 offset = curr - portalCenter;
                Vector3 offsetXZ = new Vector3(offset.x, 0f, offset.z);
                Vector3 rotatedXZ = deltaRot * offsetXZ;
                Vector3 newHorizontal = portalCenter + rotatedXZ;
                Vector3 newPos = new Vector3(newHorizontal.x, targetY + bob, newHorizontal.z);

                playerRb.transform.position = newPos;
                playerRb.transform.rotation = playerRb.transform.rotation * deltaRot;
            }
            else
            {
                Vector3 curr = player.position;
                Vector3 offset = curr - portalCenter;
                Vector3 offsetXZ = new Vector3(offset.x, 0f, offset.z);
                Vector3 rotatedXZ = deltaRot * offsetXZ;
                Vector3 newHorizontal = portalCenter + rotatedXZ;
                Vector3 newPos = new Vector3(newHorizontal.x, targetY + bob, newHorizontal.z);

                player.position = newPos;
                player.Rotate(Vector3.up * deltaAngle, Space.World);
            }

            // Hover nie przywraca fizyki automatycznie — jeśli chcesz zakończyć hover i przywrócić fizykę, dodaj warunek/wywołanie EndLift()
        }
    }

    void EndLift()
    {
        phase = Phase.Idle;

        if (playerController != null)
            playerController.movementLocked = false;

        if (playerRb != null)
        {
            // przywróć stany fizyki
            playerRb.useGravity = previousUseGravity;
            playerRb.isKinematic = previousIsKinematic;
            // zeruj prędkości aby nie dostać nagłego impulse
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        // przywróć pitch audio
        if (portalAudioSource != null)
            portalAudioSource.pitch = previousAudioPitch;

        currentAngularSpeed = 0f;
        liftElapsed = 0f;
    }
}


