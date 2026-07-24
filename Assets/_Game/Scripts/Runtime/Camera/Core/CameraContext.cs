using UnityEngine;

public readonly struct CameraContext
{
    public CameraContext(
        Vector3 playerPosition,
        Vector3 velocity,
        bool hasBallTarget,
        Vector3 ballPosition,
        float deltaTime,
        Vector3 currentCameraPosition = default,
        Vector3 cameraRight = default,
        bool isTargetBallOwner = false)
    {
        PlayerPosition = playerPosition;
        Velocity = velocity;
        HasBallTarget = hasBallTarget;
        BallPosition = ballPosition;
        DeltaTime = deltaTime;
        CurrentCameraPosition = currentCameraPosition;
        CameraRight = cameraRight.sqrMagnitude > 0.0001f ? cameraRight : Vector3.right;
        IsTargetBallOwner = isTargetBallOwner;
    }

    public Vector3 PlayerPosition { get; }
    public Vector3 Velocity { get; }
    public bool HasBallTarget { get; }
    public Vector3 BallPosition { get; }
    public float DeltaTime { get; }
    public Vector3 CurrentCameraPosition { get; }
    public Vector3 CameraRight { get; }
    public bool IsTargetBallOwner { get; }
}
