using UnityEngine;

public class PortalLift : MonoBehaviour
{
    private const float EPS = 0.0001f;
    private const float YReachTolerance = 0.05f;

    [Header("Movement")]
    public float pullSpeed = 1f;
    public float rotateSpeed = 360f;
    public float liftHeight = 2f;

    [Header("Center Move")]
    public float centerMoveDuration = 1.5f;
    public AnimationCurve centerMoveCurve = AnimationCurve.EaseInOut(0,0,1,1);

    [Header("Lift")]
    public AnimationCurve rotateSpeedCurve = AnimationCurve.EaseInOut(0,0,1,1);
    public bool useLiftYCurve = false;
    public AnimationCurve liftYCurve = AnimationCurve.EaseInOut(0,0,1,1);

    [Header("Camera")]
    public AnimationCurve cameraZeroCurve = AnimationCurve.EaseInOut(0,0,1,1);
    public AnimationCurve lookDownCurve = AnimationCurve.EaseInOut(0,0,1,1);
    public float lookDownOrbitDuration = 1.25f;
    public float cameraDownAngleDegrees = 35f;
    public float orbitSpeedDuringLookDown = 360f;

    [Header("Suck Back")]
    public float suckBackDuration = 0.35f;
    public AnimationCurve suckBackCurve = AnimationCurve.EaseInOut(0,0,1,1);
    public AudioClip suckBackClip;
    public float suckBackVolume = 1f;

    [Header("Audio")]
    public AudioSource portalAudioSource;
    public AnimationCurve audioPitchCurve = AnimationCurve.Linear(0,0,1,1);
    public float audioPitchMin = 0.8f;
    public float audioPitchMax = 1.2f;
    public bool affectDuringMoveToCenter = true;

    [Header("Refs")]
    public Transform portalVFX;

    [Header("Teleport")]
    public Transform teleportPoint;
    public RoomsController gameController;
    public bool teleportAfterSequence = true;

    private enum Phase { Idle, MoveToCenter, Lift, LookDownOrbit }
    private Phase phase = Phase.Idle;

    Transform player;
    Rigidbody playerRb;
    PlayerController playerController;

    Vector3 portalCenter;
    Vector3 moveStartPos;
    float moveElapsed;

    float startY;
    float targetY;

    bool previousUseGravity;
    bool previousIsKinematic;
    float currentAngularSpeed;

    float liftElapsed;
    float liftDuration;

    // Camera state
    Quaternion camPhaseStartLocalRot = Quaternion.identity;
    float camProgressMove;
    float camProgressLift;
    float camProgressLook;

    // LookDown & SuckBack
    float lookElapsed;
    bool isSuckingBack;
    float suckElapsed;
    Vector3 suckStartPos;
    bool suckSoundPlayed;

    // Audio state
    float previousAudioPitch;

    bool triggerLocked;

    void OnTriggerEnter(Collider other)
    {
        if (triggerLocked) return;
        if (!other.CompareTag("Player") || phase != Phase.Idle) return;

        player = other.transform;
        playerRb = other.GetComponent<Rigidbody>();
        playerController = other.GetComponent<PlayerController>();

        portalCenter = portalVFX ? portalVFX.position : transform.position;
        moveStartPos = GetPlayerPos();

        ResetPhaseProgress();
        phase = Phase.MoveToCenter;

        CacheCameraStart();
        PrepareRigidbody();
        PrepareAudio();
        LockMovement(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        triggerLocked = false;
    }

    void FixedUpdate()
    {
        if (phase == Phase.Idle || player == null) return;
        float dt = Time.fixedDeltaTime;

        switch (phase)
        {
            case Phase.MoveToCenter:
                UpdateMoveToCenter(dt);
                break;
            case Phase.Lift:
                UpdateLift(dt);
                break;
            case Phase.LookDownOrbit:
                UpdateLookDownOrbit(dt);
                break;
        }
    }

    void LateUpdate()
    {
        if (phase == Phase.Idle || playerController == null || playerController.cameraTransform == null) return;

        if (phase == Phase.MoveToCenter) UpdateCameraZero(camProgressMove);
        else if (phase == Phase.Lift)    UpdateCameraZero(camProgressLift);
        else if (phase == Phase.LookDownOrbit) UpdateCameraLookDown(camProgressLook);
    }

    // --- Phase Updates ---
    void UpdateMoveToCenter(float dt)
    {
        moveElapsed += dt;
        float t = Mathf.Clamp01(moveElapsed / Mathf.Max(EPS, centerMoveDuration));
        camProgressMove = t;

        Vector2 startXZ = new(moveStartPos.x, moveStartPos.z);
        Vector2 targetXZ = new(portalCenter.x, portalCenter.z);
        Vector2 lerpXZ = Vector2.Lerp(startXZ, targetXZ, centerMoveCurve.Evaluate(t));
        SetPlayerPos(new Vector3(lerpXZ.x, moveStartPos.y, lerpXZ.y));

        UpdateCameraZero(t);
        if (t < 1f) return;

        TransitionToLift();
    }

    void UpdateLift(float dt)
    {
        liftElapsed += dt;
        float tLift = Mathf.Clamp01(liftElapsed / Mathf.Max(EPS, liftDuration));
        camProgressLift = tLift;

        // orbit
        float rotFactor = rotateSpeedCurve.Evaluate(tLift);
        currentAngularSpeed = rotateSpeed * rotFactor;
        float deltaAngle = currentAngularSpeed * dt;
        OrbitXZ(deltaAngle);

        // vertical
        float newY = useLiftYCurve
            ? Mathf.Lerp(startY, targetY, liftYCurve.Evaluate(tLift))
            : Mathf.MoveTowards(GetPlayerPos().y, targetY, pullSpeed * dt);

        SetPlayerY(newY);
        ApplySelfRotation(deltaAngle);

        // audio
        if (portalAudioSource)
            portalAudioSource.pitch = Mathf.Lerp(audioPitchMin, audioPitchMax, audioPitchCurve.Evaluate(tLift));

        bool reachedY = useLiftYCurve ? (tLift >= 1f - EPS) : (Mathf.Abs(GetPlayerPos().y - targetY) < YReachTolerance);
        UpdateCameraZero(tLift);
        if (!reachedY) return;

        TransitionToLookDown();
    }

    void UpdateLookDownOrbit(float dt)
    {
        if (!isSuckingBack)
        {
            lookElapsed += dt;
            float tLook = Mathf.Clamp01(lookElapsed / Mathf.Max(EPS, lookDownOrbitDuration));
            camProgressLook = tLook;

            float deltaAngle = orbitSpeedDuringLookDown * dt;
            OrbitXZ(deltaAngle);
            ApplySelfRotation(deltaAngle);
            SetPlayerY(targetY);

            // camera pitching
            if (playerController?.cameraTransform)
            {
                float lookEval = lookDownCurve.Evaluate(tLook);
                Quaternion downTarget = Quaternion.Euler(cameraDownAngleDegrees, 0f, 0f);
                Quaternion zeroed = Quaternion.Slerp(camPhaseStartLocalRot, Quaternion.identity, lookEval);
                playerController.cameraTransform.localRotation =
                    Quaternion.Slerp(zeroed, downTarget, lookEval);
            }

            if (tLook >= 1f - EPS)
            {
                suckStartPos = GetPlayerPos();
                isSuckingBack = true;
                suckElapsed = 0f;
                if (portalAudioSource) portalAudioSource.pitch = audioPitchMax;
            }
            return;
        }

        // suck back
        suckElapsed += dt;
        float tSuck = Mathf.Clamp01(suckElapsed / Mathf.Max(EPS, suckBackDuration));
        float suckEval = suckBackCurve.Evaluate(tSuck);

        // orbit podczas ssania z max prędkością rotateSpeed
        float deltaAngleSuck = rotateSpeed * dt;
        OrbitXZ(deltaAngleSuck);
        ApplySelfRotation(deltaAngleSuck);

        // pozycja
        Vector3 targetPos = Vector3.Lerp(suckStartPos, moveStartPos, suckEval);
        SetPlayerPos(new Vector3(GetPlayerPos().x, targetPos.y, GetPlayerPos().z)); // Y zasysany, XZ z orbitu

        // camera domykanie z down do zero
        if (playerController?.cameraTransform)
        {
            Quaternion downTarget = Quaternion.Euler(cameraDownAngleDegrees, 0f, 0f);
            playerController.cameraTransform.localRotation =
                Quaternion.Slerp(downTarget, Quaternion.identity, suckEval);
        }

        if (tSuck < 1f) return;

        

        EndLift();
        if (teleportAfterSequence)
            DoTeleport();

        if (!suckSoundPlayed && suckBackClip)
        {
            suckSoundPlayed = true;
            // wcześniej: portalAudioSource.PlayOneShot(...)
            // teraz: odtwarzanie przy graczu aby nie ucięło po teleportacji
            Vector3 playPos = player ? player.position : portalCenter;
            AudioSource.PlayClipAtPoint(suckBackClip, playPos, suckBackVolume);
        }
        
    }

    // --- Transitions ---
    void TransitionToLift()
    {
        phase = Phase.Lift;
        currentAngularSpeed = 0f;
        startY = GetPlayerPos().y;
        targetY = startY + liftHeight;
        SetPlayerPos(new Vector3(portalCenter.x, startY, portalCenter.z));

        liftElapsed = 0f;
        liftDuration = Mathf.Max(EPS, Mathf.Abs(liftHeight) / Mathf.Max(EPS, Mathf.Abs(pullSpeed)));
        camProgressMove = 1f;
        camProgressLift = 0f;

        if (playerController?.cameraTransform)
            camPhaseStartLocalRot = playerController.cameraTransform.localRotation;
    }

    void TransitionToLookDown()
    {
        SetPlayerPos(new Vector3(portalCenter.x, targetY, portalCenter.z));
        phase = Phase.LookDownOrbit;
        currentAngularSpeed = orbitSpeedDuringLookDown;

        lookElapsed = 0f;
        suckElapsed = 0f;
        isSuckingBack = false;
        camProgressLift = 1f;
        camProgressLook = 0f;

        if (portalAudioSource)
            portalAudioSource.pitch = Mathf.Lerp(audioPitchMin, audioPitchMax, audioPitchCurve.Evaluate(1f));

        if (playerController?.cameraTransform)
            camPhaseStartLocalRot = playerController.cameraTransform.localRotation;
    }

    // --- Helpers ---
    void OrbitXZ(float deltaAngleDeg)
    {
        Quaternion deltaRot = Quaternion.Euler(0f, deltaAngleDeg, 0f);
        Vector3 pos = GetPlayerPos();
        Vector3 offset = pos - portalCenter;
        Vector3 offsetXZ = new(offset.x, 0f, offset.z);
        Vector3 rotatedXZ = deltaRot * offsetXZ;
        Vector3 newPos = new Vector3(portalCenter.x + rotatedXZ.x, pos.y, portalCenter.z + rotatedXZ.z);
        SetPlayerPos(newPos);
    }

    void ApplySelfRotation(float deltaAngleDeg)
    {
        if (playerRb) playerRb.transform.rotation *= Quaternion.Euler(0f, deltaAngleDeg, 0f);
        else if (player) player.Rotate(Vector3.up * deltaAngleDeg, Space.World);
    }

    void UpdateCameraZero(float progress01)
    {
        if (!playerController?.cameraTransform) return;
        float camT = cameraZeroCurve.Evaluate(progress01);
        playerController.cameraTransform.localRotation =
            Quaternion.Slerp(camPhaseStartLocalRot, Quaternion.identity, camT);
    }

    void UpdateCameraLookDown(float progress01)
    {
        if (!playerController?.cameraTransform) return;
        float lookT = lookDownCurve.Evaluate(progress01);
        Quaternion downTarget = Quaternion.Euler(cameraDownAngleDegrees, 0f, 0f);
        Quaternion zeroed = Quaternion.Slerp(camPhaseStartLocalRot, Quaternion.identity, lookT);
        playerController.cameraTransform.localRotation =
            Quaternion.Slerp(zeroed, downTarget, lookT);
    }

    void CacheCameraStart()
    {
        if (playerController?.cameraTransform)
        {
            camPhaseStartLocalRot = playerController.cameraTransform.localRotation;
        }
        else camPhaseStartLocalRot = Quaternion.identity;
    }

    void PrepareRigidbody()
    {
        if (!playerRb)
        {
            previousUseGravity = false;
            previousIsKinematic = false;
            return;
        }
        previousUseGravity = playerRb.useGravity;
        previousIsKinematic = playerRb.isKinematic;
        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;
        playerRb.useGravity = false;
        playerRb.isKinematic = true;
    }

    void PrepareAudio()
    {
        if (!portalAudioSource)
        {
            if (portalVFX) portalAudioSource = portalVFX.GetComponent<AudioSource>();
            if (!portalAudioSource) portalAudioSource = GetComponent<AudioSource>();
        }
        if (portalAudioSource) previousAudioPitch = portalAudioSource.pitch;
    }

    void LockMovement(bool locked)
    {
        if (playerController) playerController.movementLocked = locked;
    }

    Vector3 GetPlayerPos() => playerRb ? playerRb.transform.position : player.position;
    void SetPlayerPos(Vector3 p)
    {
        if (playerRb) playerRb.transform.position = p;
        else if (player) player.position = p;
    }
    void SetPlayerY(float y)
    {
        Vector3 p = GetPlayerPos();
        p.y = y;
        SetPlayerPos(p);
    }

    void ResetPhaseProgress()
    {
        camProgressMove = camProgressLift = camProgressLook = 0f;
        liftElapsed = moveElapsed = lookElapsed = suckElapsed = 0f;
        isSuckingBack = false;
        suckSoundPlayed = false;
    }

    // Zakończenie — jeśli chcesz użyć po dźwięku ssania
    void EndLift()
    {
        if (playerRb)
        {
            playerRb.useGravity = previousUseGravity;
            playerRb.isKinematic = previousIsKinematic;
            playerRb.angularVelocity = Vector3.zero;
            playerRb.linearVelocity = Vector3.zero;
        }

        LockMovement(false);

        if (playerController?.cameraTransform)
            playerController.cameraTransform.localRotation = Quaternion.identity;

        if (portalAudioSource)
            portalAudioSource.pitch = previousAudioPitch;

        ResetPhaseProgress();
        currentAngularSpeed = 0f;
        phase = Phase.Idle;
        // USUNIĘTO stałe blokowanie:
        // triggerLocked = true;
        triggerLocked = true; // tymczasowo, zdejmujemy w DoTeleport albo korutynie
    }

    void DoTeleport()
    {
        if (!teleportPoint) return;
        if (playerRb) playerRb.transform.position = teleportPoint.position;
        else if (player) player.position = teleportPoint.position;

        if (gameController) gameController.SwapRooms();

        // Odblokuj ponownie po zmianie pokoju
        triggerLocked = false;
    }
}


