using UnityEngine;

public readonly struct CameraContext
{
    public CameraContext(
        Vector3 playerPosition,
        Vector3 velocity,
        bool hasMoveIntent,
        Vector3 moveIntent,
        Vector3 actionIntent,
        Vector3 targetForward,
        bool hasBallTarget,
        Vector3 ballPosition,
        float currentYaw,
        float deltaTime,
        Vector3 currentCameraPosition = default,
        Vector3 cameraRight = default)
    {
        PlayerPosition = playerPosition;
        Velocity = velocity;
        HasMoveIntent = hasMoveIntent;
        MoveIntent = moveIntent;
        ActionIntent = actionIntent;
        TargetForward = targetForward;
        HasBallTarget = hasBallTarget;
        BallPosition = ballPosition;
        CurrentYaw = currentYaw;
        DeltaTime = deltaTime;
        CurrentCameraPosition = currentCameraPosition;
        CameraRight = cameraRight.sqrMagnitude > 0.0001f ? cameraRight : Vector3.right;
    }

    public Vector3 PlayerPosition { get; }
    public Vector3 Velocity { get; }
    public bool HasMoveIntent { get; }
    public Vector3 MoveIntent { get; }
    public Vector3 ActionIntent { get; }
    public Vector3 TargetForward { get; }
    public bool HasBallTarget { get; }
    public Vector3 BallPosition { get; }
    public float CurrentYaw { get; }
    public float DeltaTime { get; }
    public Vector3 CurrentCameraPosition { get; }
    public Vector3 CameraRight { get; }
}
