using Unity.VisualScripting;
using UnityEngine;

public class PortalBackLift : MonoBehaviour
{
    [Header("Center")]
    public float centerMoveDuration = 2f;

    [Header("Suck (spin)")]
    public float suckRotateSpeedDegPerSec = 360f;

    [Header("Refs")]
    public Transform portalVFX;
    public Transform CameraObject;
    enum Phase { Idle, Waiting, Suck }
    Phase phase = Phase.Idle;

    Transform player;
    Rigidbody playerRb;
    PlayerController playerController;
    float moveElapsed;
    bool previousUseGravity;
    bool previousIsKinematic;

    void OnTriggerEnter(Collider other)
    {
        if (phase != Phase.Idle) return;

        Rigidbody rb = other.attachedRigidbody ?? other.GetComponentInParent<Rigidbody>();
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        Transform playerT = rb ? rb.transform : other.GetComponentInParent<Transform>();

        if (pc == null || playerT == null) return;
        if (!playerT.CompareTag("Player") && !playerT.root.CompareTag("Player")) return;

        player = playerT;
        playerRb = rb;
        playerController = pc;

        // natychmiastowo wyzeruj rotację gracza i kamery
        player.rotation = Quaternion.Euler(0f, 0f, 0f);
        CameraObject.rotation = Quaternion.Euler(0f, 0f, 0f);
        if (playerController)
            playerController.ForceLook(0f, 0f);


        moveElapsed = 0f;
        PrepareRigidbody();
        LockMovement(true);
    }

    void OnTriggerExit(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody ?? other.GetComponentInParent<Rigidbody>();
        if (rb == null) return;
        if (playerRb != null && rb == playerRb)
        {
            RestoreRigidbody();
            LockMovement(false);
            phase = Phase.Idle;
            player = null;
            playerRb = null;
            playerController = null;
        }
    }

    void FixedUpdate()
    {
        if (phase == Phase.Idle) return;

        float dt = Time.fixedDeltaTime;

        if (phase == Phase.Waiting)
        {
            moveElapsed += dt;
            if (moveElapsed >= centerMoveDuration)
                phase = Phase.Suck;
        }
        else if (phase == Phase.Suck)
        {
            // obracamy gracza zamiast kamery (oś Z - "suck")
            if (player != null)
                player.Rotate(Vector3.forward, suckRotateSpeedDegPerSec * dt, Space.Self);
        }
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

    void RestoreRigidbody()
    {
        if (!playerRb) return;
        playerRb.useGravity = previousUseGravity;
        playerRb.isKinematic = previousIsKinematic;
        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;
    }

    void LockMovement(bool locked)
    {
        if (playerController) playerController.movementLocked = locked;
    }
}


