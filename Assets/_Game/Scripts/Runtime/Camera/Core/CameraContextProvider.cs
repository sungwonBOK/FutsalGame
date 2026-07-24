using UnityEngine;

public sealed class CameraContextProvider
{
    private Transform playerTarget;
    private Rigidbody playerRigidbody;
    private PlayerBallHandler playerBallHandler;
    private Transform ballTarget;
    private BallController ballController;
    private readonly Transform cameraTransform;

    public CameraContextProvider(
        Transform playerTarget,
        Rigidbody playerRigidbody,
        Transform ballTarget,
        Transform cameraTransform)
    {
        this.playerTarget = playerTarget;
        this.playerRigidbody = playerRigidbody;
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
        if (playerBallHandler == null && playerTarget != null)
            playerBallHandler = playerTarget.GetComponent<PlayerBallHandler>();
        if (ballController == null && ballTarget != null)
            ballController = ballTarget.GetComponent<BallController>();
    }

    public void SetTargets(Transform player, Rigidbody playerBody, Transform ball)
    {
        playerTarget = player;
        playerRigidbody = playerBody;
        playerBallHandler = playerTarget != null ? playerTarget.GetComponent<PlayerBallHandler>() : null;
        ballTarget = ball;
        ballController = ballTarget != null ? ballTarget.GetComponent<BallController>() : null;
    }

    public bool TryGet(float deltaTime, out CameraContext context)
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
            hasBallTarget: ballTarget != null,
            ballPosition: ballTarget != null ? ballTarget.position : playerTarget.position,
            deltaTime: Mathf.Max(deltaTime, 0.0001f),
            currentCameraPosition: cameraTransform != null ? cameraTransform.position : Vector3.zero,
            cameraRight: cameraTransform != null ? cameraTransform.right : Vector3.right,
            isTargetBallOwner: ballController != null && ballController.CurrentOwner == playerBallHandler);
        return true;
    }
}
