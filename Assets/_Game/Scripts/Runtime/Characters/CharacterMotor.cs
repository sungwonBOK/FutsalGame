using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CharacterState))]
public class CharacterMotor : MonoBehaviour
{
    [Header("Legacy Movement Defaults")]
    [Tooltip("Fallback movement speed for older AI/component callers.")]
    [SerializeField] private float moveSpeed = 6f;

    [Tooltip("Fallback rotation speed for older AI/component callers.")]
    [SerializeField] private float turnSpeed = 720f;

    private Rigidbody rb;
    private CharacterState state;
    private Vector3 moveDirection;
    private CharacterMovementProfile activeMovementProfile;

    private Vector3 dashVelocity;
    private float dashUntil = -999f;

    public Vector3 MoveDirection => moveDirection;
    public bool HasMoveInput => moveDirection.sqrMagnitude > 0.0001f;
    public CharacterMovementProfile ActiveMovementProfile => activeMovementProfile;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        state = GetComponent<CharacterState>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        activeMovementProfile = CharacterMovementUtility.SanitizeProfile(
            new CharacterMovementProfile(moveSpeed, moveSpeed * 8f, moveSpeed * 10f, turnSpeed),
            moveSpeed,
            turnSpeed);
    }

    public void SetMovement(Vector3 direction, CharacterMovementProfile profile)
    {
        moveDirection = CharacterMovementUtility.ClampPlanar(direction);
        activeMovementProfile = CharacterMovementUtility.SanitizeProfile(profile, moveSpeed, turnSpeed);
    }

    public void Dash(Vector3 velocity, float duration)
    {
        dashVelocity = velocity;
        dashVelocity.y = 0f;
        dashUntil = Time.time + duration;
    }

    private void FixedUpdate()
    {
        if (state != null && state.IsStunned)
            return;

        Vector3 current = rb.linearVelocity;
        rb.angularVelocity = Vector3.zero;

        if (Time.time < dashUntil)
        {
            rb.linearVelocity = new Vector3(dashVelocity.x, current.y, dashVelocity.z);
            return;
        }

        CharacterMovementProfile profile = CharacterMovementUtility.SanitizeProfile(activeMovementProfile, moveSpeed, turnSpeed);
        Vector3 currentPlanarVelocity = new Vector3(current.x, 0f, current.z);
        Vector3 targetPlanarVelocity = moveDirection * profile.speed;
        float rate = targetPlanarVelocity.sqrMagnitude > currentPlanarVelocity.sqrMagnitude
            ? profile.acceleration
            : profile.deceleration;
        Vector3 nextPlanarVelocity = Vector3.MoveTowards(
            currentPlanarVelocity,
            targetPlanarVelocity,
            rate * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(nextPlanarVelocity.x, current.y, nextPlanarVelocity.z);

        if (HasMoveInput)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDirection, Vector3.up);
            Quaternion nextRotation = Quaternion.RotateTowards(
                rb.rotation,
                targetRot,
                profile.rotationSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(nextRotation);
        }
    }

    public static Vector3 BuildPlanarMoveDirection(Vector2 input)
    {
        return CharacterMovementUtility.BuildPlanarMoveDirection(input);
    }

    public static Vector3 ResolveActionDirection(bool hasMoveInput, Vector3 moveDirection, Vector3 characterForward)
    {
        return CharacterMovementUtility.ResolveActionDirection(hasMoveInput, moveDirection, characterForward);
    }

    public static Vector3 NormalizePlanar(Vector3 direction)
    {
        return CharacterMovementUtility.NormalizePlanar(direction);
    }
}
