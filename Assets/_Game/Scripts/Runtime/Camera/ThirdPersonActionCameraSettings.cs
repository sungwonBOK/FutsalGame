using UnityEngine;

[CreateAssetMenu(menuName = "Futsal Brawl/Camera/Third Person Action Camera Settings")]
public class ThirdPersonActionCameraSettings : ScriptableObject
{
    [Header("Follow")]
    [Min(0.1f)] public float distance = 7.2f;
    [Min(0f)] public float height = 4.1f;
    [Min(0f)] public float lookAtHeight = 1.7f;
    [Min(0.01f)] public float positionSmoothTime = 0.12f;

    [Header("Rotation")]
    [Min(0.01f)] public float rotationSmoothTime = 0.24f;
    [Min(1f)] public float maxRotationSpeed = 220f;
    [Min(0f)] public float rotationDeadZone = 8f;
    [Min(0f)] public float movementPrioritySpeed = 0.75f;

    [Header("Ball Assist")]
    [Range(0f, 1f)] public float ballAssistStrength = 0.22f;
    [Range(0f, 120f)] public float ballAssistEdgeAngle = 35f;
    [Range(45f, 179f)] public float ballAssistMaxAngle = 120f;

    [Header("Collision")]
    [Min(0.05f)] public float collisionRadius = 0.35f;
    [Min(0.5f)] public float minCollisionDistance = 1.25f;
    [Min(0.01f)] public float collisionMoveInSmoothTime = 0.06f;
    [Min(0.01f)] public float collisionReturnSmoothTime = 0.35f;
    public LayerMask collisionMask = ~0;

    [Header("Camera Shake")]
    [Min(0f)] public float shakeStrength = 0.18f;
    [Min(1f)] public float shakeFrequency = 28f;
    [Min(0f)] public float maxShakeOffset = 0.22f;
    [Min(0f)] public float maxShakeAngle = 1.6f;
    [Min(0.01f)] public float shakeDecay = 7f;

    [Header("FOV")]
    [Range(40f, 120f)] public float baseFov = 85f;
    [Range(0f, 8f)] public float sprintFovBoost = 4f;
    [Min(0.1f)] public float sprintSpeed = 9f;
    [Min(0.01f)] public float fovSmoothTime = 0.18f;
}
