using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private Transform movementReference;
    [SerializeField] private PlayerActionBindings actionBindings;

    private CharacterLocomotion locomotion;
    private CombatController combat;
    private PlayerBallHandler ball;
    private CharacterState state;
    private PlayerActionBindings runtimeActionBindings;

    private PlayerActionBindings ActionBindings
    {
        get
        {
            if (actionBindings != null)
                return actionBindings;

            if (runtimeActionBindings == null)
                runtimeActionBindings = ScriptableObject.CreateInstance<PlayerActionBindings>();
            return runtimeActionBindings;
        }
    }

    private void Awake()
    {
        locomotion = GetComponent<CharacterLocomotion>();
        if (locomotion == null)
            locomotion = gameObject.AddComponent<CharacterLocomotion>();

        combat = GetComponent<CombatController>();
        ball = GetComponent<PlayerBallHandler>();
        state = GetComponent<CharacterState>();

        if (movementReference == null && Camera.main != null)
            movementReference = Camera.main.transform;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null && Mouse.current == null)
            return;

        if (!GameManager.PlayActive || (state != null && state.IsStunned))
        {
            locomotion.SetPlayerMoveInput(Vector2.zero, sprint: false, hasBall: ball != null && ball.HasBall);
            if (ball != null)
                ball.SetSprintDribbleInput(false, Vector3.zero);
            return;
        }

        Vector2 moveInput = BuildMoveInput(
            kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed),
            kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed),
            kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed),
            kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed));

        bool sprint = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        bool hasBall = ball != null && ball.HasBall;
        Vector3 moveDirection = BuildCameraRelativeMoveDirection(moveInput, movementReference);
        locomotion.SetPlayerMoveInput(moveInput, moveDirection, sprint, hasBall);

        Vector3 actionDirection = locomotion.ActionDirection;
        if (kb != null && kb.jKey.wasPressedThisFrame && combat != null)
            combat.Punch(actionDirection);
        if (kb != null && kb.kKey.wasPressedThisFrame && combat != null)
            combat.SlideTackle(actionDirection);

        if (ball != null)
        {
            ball.SetSprintDribbleInput(sprint, actionDirection);
            HandleBallActions();
        }
    }

    public static Vector2 BuildMoveInput(bool leftPressed, bool rightPressed, bool downPressed, bool upPressed)
    {
        Vector2 input = Vector2.zero;
        if (leftPressed)
            input.x -= 1f;
        if (rightPressed)
            input.x += 1f;
        if (downPressed)
            input.y -= 1f;
        if (upPressed)
            input.y += 1f;

        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    public static Vector3 BuildCameraRelativeMoveDirection(Vector2 input, Transform reference)
    {
        return CharacterMovementUtility.BuildCameraRelativeMoveDirection(input, reference);
    }

    public static Vector3 BuildPlanarCameraForward(Transform reference, Vector3 fallbackForward)
    {
        Vector3 direction = reference != null ? reference.forward : fallbackForward;
        direction.y = 0f;
        direction = direction.normalized;
        if (direction.sqrMagnitude > 0.0001f)
            return direction;

        fallbackForward.y = 0f;
        return fallbackForward.sqrMagnitude > 0.0001f ? fallbackForward.normalized : Vector3.forward;
    }

    private void HandleBallActions()
    {
        ActionButtonState cancel = PlayerActionInputReader.Read(ActionBindings.Cancel);
        ActionButtonState pass = PlayerActionInputReader.Read(ActionBindings.Pass);
        ActionButtonState shot = PlayerActionInputReader.Read(ActionBindings.Shot);

        if (cancel.WasPressed)
        {
            ball.CancelCharge();
            return;
        }

        if (ball.IsCharging)
        {
            Vector3 cameraForward = BuildPlanarCameraForward(movementReference, transform.forward);
            if (pass.WasReleased)
                ball.ReleaseCharge(BallChargeAction.Pass, cameraForward);
            if (shot.WasReleased)
                ball.ReleaseCharge(BallChargeAction.Shot, cameraForward);
            return;
        }

        if (pass.WasPressed)
            ball.StartCharge(BallChargeAction.Pass);
        else if (shot.WasPressed)
            ball.StartCharge(BallChargeAction.Shot);
    }

    private void OnDestroy()
    {
        if (runtimeActionBindings != null)
            Destroy(runtimeActionBindings);
    }
}
