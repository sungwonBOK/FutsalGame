using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterMotor))]
[RequireComponent(typeof(CharacterState))]
public class CharacterLocomotion : MonoBehaviour
{
    [SerializeField] private CharacterMovementConfig config;

    private CharacterMotor motor;
    private CharacterState state;
    private CharacterMovementConfig runtimeConfig;
    private Vector2 rawMoveInput;
    private Vector3 moveDirection;
    private Vector3 actionDirection = Vector3.forward;
    private CharacterMovementProfile activeMovementProfile;

    public CharacterMovementConfig Config
    {
        get
        {
            if (config == null)
            {
                if (runtimeConfig == null)
                    runtimeConfig = ScriptableObject.CreateInstance<CharacterMovementConfig>();
                return runtimeConfig;
            }

            return config;
        }
    }

    public Vector2 RawMoveInput => rawMoveInput;
    public Vector3 MoveDirection => moveDirection;
    public bool HasMoveInput => moveDirection.sqrMagnitude > 0.0001f;
    public CharacterMovementProfile ActiveMovementProfile => activeMovementProfile;

    public Vector3 ActionDirection
    {
        get
        {
            Vector3 fallbackForward = CharacterMovementUtility.FlattenOrFallback(transform.forward, Vector3.forward);
            return CharacterMovementUtility.FlattenOrFallback(actionDirection, fallbackForward);
        }
    }

    private void Awake()
    {
        motor = GetComponent<CharacterMotor>();
        state = GetComponent<CharacterState>();
        activeMovementProfile = Config.ResolveProfile(sprint: false, hasBall: false);
        actionDirection = CharacterMovementUtility.FlattenOrFallback(transform.forward, Vector3.forward);
        motor.SetMovement(Vector3.zero, activeMovementProfile);
    }

    private void OnDestroy()
    {
        if (runtimeConfig != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeConfig);
            else
                DestroyImmediate(runtimeConfig);
        }
    }

    public void SetMoveInput(Vector3 direction)
    {
        direction.y = 0f;
        rawMoveInput = CharacterMovementUtility.ClampInput(new Vector2(direction.x, direction.z));
        ApplyMovement(rawMoveInput, CharacterMovementUtility.ClampPlanar(direction), sprint: false, hasBall: false);
    }

    public void SetPlayerMoveInput(Vector2 input, bool sprint, bool hasBall)
    {
        ApplyMovement(
            CharacterMovementUtility.ClampInput(input),
            CharacterMovementUtility.BuildPlanarMoveDirection(input),
            sprint,
            hasBall);
    }

    public void SetPlayerMoveInput(Vector2 input, Vector3 worldMoveDirection, bool sprint, bool hasBall)
    {
        ApplyMovement(
            CharacterMovementUtility.ClampInput(input),
            CharacterMovementUtility.ClampPlanar(worldMoveDirection),
            sprint,
            hasBall);
    }

    private void ApplyMovement(Vector2 input, Vector3 direction, bool sprint, bool hasBall)
    {
        rawMoveInput = input;
        moveDirection = state != null && state.IsStunned ? Vector3.zero : direction;
        activeMovementProfile = Config.ResolveProfile(sprint, hasBall);
        actionDirection = CharacterMovementUtility.ResolveActionDirection(HasMoveInput, moveDirection, transform.forward);
        motor.SetMovement(moveDirection, activeMovementProfile);
    }
}
