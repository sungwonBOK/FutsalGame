using UnityEngine;

public sealed class ContextualPlayerActionRouter
{
    private enum ChargeInputButton
    {
        None,
        Primary,
        Secondary
    }

    private readonly CharacterLocomotion locomotion;
    private readonly CombatController combat;
    private readonly PlayerBallHandler ball;
    private readonly OneTouchIntentBuffer oneTouchBuffer = new OneTouchIntentBuffer();
    private readonly OneTouchActionExecutor oneTouchExecutor = new OneTouchActionExecutor();
    private readonly PossessionInputContext possessionContext = new PossessionInputContext();

    private BallChargeAction pendingChargeAction;
    private ChargeInputButton pendingChargeButton;
    private BallChargeAction activeChargeAction;
    private ChargeInputButton activeChargeButton;

    public ContextualPlayerActionRouter(
        CharacterLocomotion locomotion,
        CombatController combat,
        PlayerBallHandler ball)
    {
        this.locomotion = locomotion;
        this.combat = combat;
        this.ball = ball;
    }

    public bool IsPreparingOneTouch => oneTouchBuffer.IsPreparing;

    public void Process(
        GameplayInputReader inputReader,
        Vector3 characterActionDirection,
        Vector3 ballAimDirection)
    {
        if (inputReader == null)
            return;

        GameplayInputButtonState primary = inputReader.ReadButton(GameplayInputAction.PrimaryAction);
        GameplayInputButtonState secondary = inputReader.ReadButton(GameplayInputAction.SecondaryAction);
        GameplayInputButtonState contextF = inputReader.ReadButton(GameplayInputAction.ContextF);

        bool actuallyHasBall = ball != null && ball.HasBall;
        bool opponentHasBall = !actuallyHasBall && PlayerBallHandler.CurrentOwner != null;
        bool withinAcquireRange = ball != null && ball.IsWithinAcquireRange;
        bool sprintHeld = inputReader.ReadButton(GameplayInputAction.Sprint).IsPressed;
        possessionContext.Update(Time.time, actuallyHasBall, opponentHasBall, withinAcquireRange, sprintHeld);
        bool mouseActionsBlocked = possessionContext.AreMouseActionsBlocked(Time.time);

        if (inputReader.ReadButton(GameplayInputAction.CancelAction).WasPressed)
        {
            ball?.CancelCharge();
            oneTouchBuffer.Clear();
            ClearChargeInput();
            return;
        }

        if ((!actuallyHasBall || !mouseActionsBlocked)
            && (TryQueueOneTouch(inputReader, GameplayInputAction.QueueOneTouchPass, OneTouchIntent.Pass, ballAimDirection)
                || TryQueueOneTouch(inputReader, GameplayInputAction.QueueOneTouchShot, OneTouchIntent.Shot, ballAimDirection)))
        {
            return;
        }

        if ((!actuallyHasBall || !mouseActionsBlocked)
            && oneTouchExecutor.TryExecuteQueued(oneTouchBuffer, ball, ballAimDirection))
            return;

        if (inputReader.ReadButton(GameplayInputAction.Dodge).WasPressed)
            locomotion?.TryDodge(characterActionDirection);

        if (contextF.WasPressed)
        {
            if (!possessionContext.HasPossessionContext)
            {
                combat?.SlideTackle(characterActionDirection);
                possessionContext.BeginCombatProtection(Time.time);
            }
            return;
        }

        if (TryHandleActiveCharge(primary, secondary, ballAimDirection))
            return;

        if (TryStartPendingCharge(primary, secondary))
            return;

        if (ball != null && ball.IsCharging)
        {
            return;
        }

        if (primary.WasPressed)
        {
            if (possessionContext.HasPossessionContext)
            {
                if (!mouseActionsBlocked)
                    BeginChargeInput(BallChargeAction.Pass, ChargeInputButton.Primary);
            }
            else
            {
                combat?.Punch(characterActionDirection);
                possessionContext.BeginCombatProtection(Time.time);
            }
        }
        else if (secondary.WasPressed)
        {
            if (possessionContext.HasPossessionContext)
            {
                if (!mouseActionsBlocked)
                    BeginChargeInput(BallChargeAction.Shot, ChargeInputButton.Secondary);
            }
            else
            {
                combat?.CrossPunch(characterActionDirection);
                possessionContext.BeginCombatProtection(Time.time);
            }
        }
    }

    public void ClearPreparedActions()
    {
        oneTouchBuffer.Clear();
        ball?.CancelCharge();
        ClearChargeInput();
        possessionContext.Clear();
    }

    private bool TryQueueOneTouch(
        GameplayInputReader inputReader,
        GameplayInputAction inputAction,
        OneTouchIntent intent,
        Vector3 actionDirection)
    {
        if (!inputReader.ReadButton(inputAction).WasPressed)
            return false;

        oneTouchBuffer.Queue(intent);
        if (oneTouchExecutor.TryAttempt(intent, ball, actionDirection))
            oneTouchBuffer.Consume();

        return true;
    }

    private bool TryHandleActiveCharge(
        GameplayInputButtonState primary,
        GameplayInputButtonState secondary,
        Vector3 ballAimDirection)
    {
        if (activeChargeAction == BallChargeAction.None)
            return false;

        bool wasReleased = activeChargeButton == ChargeInputButton.Primary
            ? primary.WasReleased
            : secondary.WasReleased;
        if (!wasReleased)
            return true;

        ball?.ReleaseCharge(activeChargeAction, ballAimDirection);
        activeChargeAction = BallChargeAction.None;
        activeChargeButton = ChargeInputButton.None;
        return true;
    }

    private bool TryStartPendingCharge(GameplayInputButtonState primary, GameplayInputButtonState secondary)
    {
        if (pendingChargeAction == BallChargeAction.None)
            return false;

        bool isHeld = pendingChargeButton == ChargeInputButton.Primary
            ? primary.IsPressed
            : secondary.IsPressed;
        if (!isHeld)
        {
            pendingChargeAction = BallChargeAction.None;
            pendingChargeButton = ChargeInputButton.None;
            return true;
        }

        if (ball == null || !ball.HasBall)
            return true;

        ball.StartCharge(pendingChargeAction);
        if (ball.IsCharging)
        {
            activeChargeAction = pendingChargeAction;
            activeChargeButton = pendingChargeButton;
        }

        pendingChargeAction = BallChargeAction.None;
        pendingChargeButton = ChargeInputButton.None;
        return true;
    }

    private void BeginChargeInput(BallChargeAction action, ChargeInputButton button)
    {
        if (ball != null && ball.HasBall)
        {
            ball.StartCharge(action);
            if (ball.IsCharging)
            {
                activeChargeAction = action;
                activeChargeButton = button;
            }
            return;
        }

        pendingChargeAction = action;
        pendingChargeButton = button;
    }

    private void ClearChargeInput()
    {
        pendingChargeAction = BallChargeAction.None;
        pendingChargeButton = ChargeInputButton.None;
        activeChargeAction = BallChargeAction.None;
        activeChargeButton = ChargeInputButton.None;
    }
}
