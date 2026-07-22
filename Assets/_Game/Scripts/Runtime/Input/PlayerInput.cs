using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private Transform movementReference;

    private CharacterLocomotion locomotion;
    private CombatController combat;
    private PlayerBallHandler ball;
    private CharacterState state;

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
        if (kb == null)
            return;

        if (!GameManager.PlayActive || (state != null && state.IsStunned))
        {
            locomotion.SetPlayerMoveInput(Vector2.zero, sprint: false, hasBall: ball != null && ball.HasBall);
            if (ball != null)
                ball.SetSprintDribbleInput(false, Vector3.zero);
            return;
        }

        Vector2 moveInput = BuildMoveInput(
            kb.aKey.isPressed || kb.leftArrowKey.isPressed,
            kb.dKey.isPressed || kb.rightArrowKey.isPressed,
            kb.sKey.isPressed || kb.downArrowKey.isPressed,
            kb.wKey.isPressed || kb.upArrowKey.isPressed);

        bool sprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        bool hasBall = ball != null && ball.HasBall;
        Vector3 moveDirection = BuildCameraRelativeMoveDirection(moveInput, movementReference);
        locomotion.SetPlayerMoveInput(moveInput, moveDirection, sprint, hasBall);

        Vector3 actionDirection = locomotion.ActionDirection;
        if (kb.jKey.wasPressedThisFrame && combat != null)
            combat.Punch(actionDirection);
        if (kb.kKey.wasPressedThisFrame && combat != null)
            combat.SlideTackle(actionDirection);

        if (ball != null)
        {
            ball.SetSprintDribbleInput(sprint, actionDirection);
            if (kb.fKey.wasPressedThisFrame)
                ball.Pass(actionDirection);
            if (kb.spaceKey.wasPressedThisFrame)
                ball.StartCharge(actionDirection);
            if (kb.spaceKey.wasReleasedThisFrame)
                ball.ReleaseCharge();
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
}
