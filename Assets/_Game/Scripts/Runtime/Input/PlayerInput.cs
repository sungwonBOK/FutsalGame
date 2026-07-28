using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    private const float SprintBoostDoubleTapWindow = 0.25f;

    [SerializeField] private Transform movementReference;
    [SerializeField] private GameplayInputReader inputReader;

    private CharacterLocomotion locomotion;
    private CombatController combat;
    private PlayerBallHandler ball;
    private CharacterState state;
    private ContextualPlayerActionRouter actionRouter;
    private float lastSprintPressTime = -1f;
    private bool burstSprintRequested;

    private void Awake()
    {
        locomotion = GetComponent<CharacterLocomotion>();
        if (locomotion == null)
            locomotion = gameObject.AddComponent<CharacterLocomotion>();

        combat = GetComponent<CombatController>();
        ball = GetComponent<PlayerBallHandler>();
        state = GetComponent<CharacterState>();
        actionRouter = new ContextualPlayerActionRouter(locomotion, combat, ball);

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
        GameplayInputButtonState sprintButton = inputReader != null
            ? inputReader.ReadButton(GameplayInputAction.Sprint)
            : default;
        bool sprint = sprintButton.IsPressed;
        UpdateBurstSprintRequest(sprintButton);
        bool hasBall = ball != null && ball.HasBall;
        Vector3 moveDirection = BuildCameraRelativeMoveDirection(moveInput, movementReference);
        locomotion.SetPlayerMoveInput(moveInput, moveDirection, sprint, hasBall, burstSprintRequested);

        Vector3 characterActionDirection = locomotion.ActionDirection;
        Vector3 ballAimDirection = BuildPlanarCameraForward(movementReference, transform.forward);

        if (ball != null)
            ball.SetSprintDribbleInput(sprint, characterActionDirection, burstSprintRequested);

        actionRouter?.Process(inputReader, characterActionDirection, ballAimDirection);
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

    public void ClearPreparedActions()
    {
        actionRouter?.ClearPreparedActions();
    }

    public static bool IsSprintBoostTap(float now, float previousPressTime)
    {
        return previousPressTime >= 0f
            && now >= previousPressTime
            && now - previousPressTime <= SprintBoostDoubleTapWindow;
    }

    private void OnDisable()
    {
        burstSprintRequested = false;
        lastSprintPressTime = -1f;
        ClearPreparedActions();
    }

    private void UpdateBurstSprintRequest(GameplayInputButtonState sprintButton)
    {
        if (sprintButton.WasPressed)
        {
            burstSprintRequested = IsSprintBoostTap(Time.time, lastSprintPressTime);
            lastSprintPressTime = Time.time;
        }

        if (!sprintButton.IsPressed)
            burstSprintRequested = false;
    }
}
