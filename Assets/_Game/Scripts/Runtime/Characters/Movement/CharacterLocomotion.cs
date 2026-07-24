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
    private bool sprintRequested;
    private bool hasBall;
    private float stamina;
    private float lastStaminaSpendTime = -999f;
    private float lastDodgeTime = -999f;
    private float dodgeUntil = -999f;

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
    public float Stamina01 => Config.MaxStamina <= 0f ? 0f : Mathf.Clamp01(stamina / Config.MaxStamina);
    public bool IsSprinting { get; private set; }
    public bool IsDodging => Time.time < dodgeUntil;
    public float DodgeRemaining => Mathf.Max(0f, Config.DodgeCooldown - (Time.time - lastDodgeTime));
    public float DodgeCooldown01 => Config.DodgeCooldown <= 0f ? 0f : Mathf.Clamp01(DodgeRemaining / Config.DodgeCooldown);
    public bool CanDodge => state != null && !state.IsStunned && !motor.IsDashing && DodgeRemaining <= 0f && stamina >= Config.DodgeCost;
    public bool DodgeBlockedByStamina => DodgeRemaining <= 0f && stamina < Config.DodgeCost;
    public float LastDodgeRejectedTime { get; private set; } = -999f;

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
        stamina = Config.MaxStamina;
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
        sprintRequested = sprint;
        this.hasBall = hasBall;
        moveDirection = state != null && state.IsStunned ? Vector3.zero : direction;
        RefreshMovementProfile();
        actionDirection = CharacterMovementUtility.ResolveActionDirection(HasMoveInput, moveDirection, transform.forward);
        motor.SetMovement(moveDirection, activeMovementProfile);
    }

    public bool TryDodge(Vector3 direction)
    {
        if (!CanDodge)
        {
            LastDodgeRejectedTime = Time.time;
            return false;
        }

        direction = CharacterMovementUtility.FlattenOrFallback(direction, -transform.forward);
        SpendStamina(Config.DodgeCost);
        lastDodgeTime = Time.time;
        dodgeUntil = Time.time + Config.DodgeDuration;
        state.SetInvulnerable(Config.DodgeInvulnerability);
        motor.Dash(direction * Config.DodgeSpeed, Config.DodgeDuration);
        RefreshMovementProfile();
        return true;
    }

    public void ResetMobilityState()
    {
        stamina = Config.MaxStamina;
        lastStaminaSpendTime = -999f;
        lastDodgeTime = -999f;
        dodgeUntil = -999f;
        sprintRequested = false;
        IsSprinting = false;
        motor.CancelDash();
        RefreshMovementProfile();
    }

    private void Update()
    {
        bool wasSprinting = IsSprinting;
        RefreshMovementProfile();

        if (IsSprinting)
        {
            SpendStamina(Config.SprintDrainPerSecond * Time.deltaTime);
        }
        else if (Time.time - lastStaminaSpendTime >= Config.StaminaRegenDelay)
        {
            stamina = Mathf.Min(Config.MaxStamina, stamina + Config.StaminaRegenPerSecond * Time.deltaTime);
        }

        if (wasSprinting != IsSprinting)
            motor.SetMovement(moveDirection, activeMovementProfile);
    }

    private void RefreshMovementProfile()
    {
        bool canStartSprint = stamina > (IsSprinting ? 0f : Config.MinStaminaToSprint);
        IsSprinting = sprintRequested && HasMoveInput && !hasBall && !IsDodging && state != null && !state.IsStunned && canStartSprint;
        activeMovementProfile = Config.ResolveProfile(IsSprinting, hasBall);
    }

    private void SpendStamina(float amount)
    {
        stamina = Mathf.Max(0f, stamina - Mathf.Max(0f, amount));
        lastStaminaSpendTime = Time.time;
    }
}
