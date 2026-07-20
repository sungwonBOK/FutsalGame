using UnityEngine;

public sealed class CameraContextProvider
{
    private Transform playerTarget;
    private Rigidbody playerRigidbody;
    private CharacterLocomotion playerLocomotion;
    private Transform ballTarget;
    private readonly Transform cameraTransform;

    public CameraContextProvider(
        Transform playerTarget,
        Rigidbody playerRigidbody,
        CharacterLocomotion playerLocomotion,
        Transform ballTarget,
        Transform cameraTransform)
    {
        this.playerTarget = playerTarget;
        this.playerRigidbody = playerRigidbody;
        this.playerLocomotion = playerLocomotion;
        this.ballTarget = ballTarget;
        this.cameraTransform = cameraTransform;
    }

    public void ResolveMissingTargets()
    {
        if (playerTarget == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
                playerTarget = player.transform;
        }

        if (ballTarget == null)
        {
            GameObject ball = GameObject.Find("Ball");
            if (ball != null)
                ballTarget = ball.transform;
        }

        if (playerRigidbody == null && playerTarget != null)
            playerRigidbody = playerTarget.GetComponent<Rigidbody>();
        if (playerLocomotion == null && playerTarget != null)
            playerLocomotion = ResolveLocomotion(playerTarget);
    }

    public void SetTargets(Transform player, Rigidbody playerBody, Transform ball)
    {
        playerTarget = player;
        playerRigidbody = playerBody;
        playerLocomotion = ResolveLocomotion(playerTarget);
        ballTarget = ball;
    }

    public bool TryGet(float currentYaw, float deltaTime, out CameraContext context)
    {
        ResolveMissingTargets();
        if (playerTarget == null)
        {
            context = default;
            return false;
        }

        context = new CameraContext(
            playerPosition: playerTarget.position,
            velocity: playerRigidbody != null ? playerRigidbody.linearVelocity : Vector3.zero,
            hasMoveIntent: playerLocomotion != null && playerLocomotion.HasMoveInput,
            moveIntent: playerLocomotion != null ? playerLocomotion.MoveDirection : Vector3.zero,
            actionIntent: playerLocomotion != null ? playerLocomotion.ActionDirection : playerTarget.forward,
            targetForward: playerTarget.forward,
            hasBallTarget: ballTarget != null,
            ballPosition: ballTarget != null ? ballTarget.position : playerTarget.position,
            currentYaw: currentYaw,
            deltaTime: Mathf.Max(deltaTime, 0.0001f),
            currentCameraPosition: cameraTransform != null ? cameraTransform.position : Vector3.zero,
            cameraRight: cameraTransform != null ? cameraTransform.right : Vector3.right);
        return true;
    }

    private static CharacterLocomotion ResolveLocomotion(Transform target)
    {
        if (target == null)
            return null;

        CharacterLocomotion locomotion = target.GetComponent<CharacterLocomotion>();
        if (locomotion != null)
            return locomotion;

        return target.GetComponent<CharacterMotor>() != null
            ? target.gameObject.AddComponent<CharacterLocomotion>()
            : null;
    }
}
