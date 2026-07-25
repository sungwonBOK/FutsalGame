using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private Transform movementReference;
    [SerializeField] private GameplayInputReader inputReader;

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

        if (inputReader == null)
            inputReader = FindAnyObjectByType<GameplayInputReader>();

        if (movementReference == null && Camera.main != null)
            movementReference = Camera.main.transform;
    }

    private void Update()
    {
        if (!GameManager.PlayActive || (state != null && state.IsStunned))
        {
            locomotion.SetPlayerMoveInput(Vector2.zero, sprint: false, hasBall: ball != null && ball.HasBall);
            if (ball != null)
                ball.SetSprintDribbleInput(false, Vector3.zero);
            return;
        }

        Vector2 moveInput = inputReader != null ? inputReader.ReadMove() : Vector2.zero;
        bool sprint = inputReader != null && inputReader.ReadButton(GameplayInputAction.Sprint).IsPressed;
        bool hasBall = ball != null && ball.HasBall;
        Vector3 moveDirection = BuildCameraRelativeMoveDirection(moveInput, movementReference);
        locomotion.SetPlayerMoveInput(moveInput, moveDirection, sprint, hasBall);

        Vector3 actionDirection = locomotion.ActionDirection;
        if (inputReader != null && inputReader.ReadButton(GameplayInputAction.Dodge).WasPressed)
            locomotion.TryDodge(actionDirection);
        if (inputReader != null && inputReader.ReadButton(GameplayInputAction.Punch).WasPressed && combat != null)
            combat.Punch(actionDirection);
        if (inputReader != null && inputReader.ReadButton(GameplayInputAction.SlideTackle).WasPressed && combat != null)
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
        GameplayInputButtonState cancel = inputReader != null
            ? inputReader.ReadButton(GameplayInputAction.CancelCharge)
            : default;
        GameplayInputButtonState pass = inputReader != null
            ? inputReader.ReadButton(GameplayInputAction.Pass)
            : default;
        GameplayInputButtonState shot = inputReader != null
            ? inputReader.ReadButton(GameplayInputAction.Shot)
            : default;

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

}
