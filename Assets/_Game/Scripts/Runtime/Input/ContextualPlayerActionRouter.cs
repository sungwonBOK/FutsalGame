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
    private readonly PowerActivationController powerActivation;
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
        PlayerBallHandler ball,
        PowerActivationController powerActivation = null)
    {
        this.locomotion = locomotion;
        this.combat = combat;
        this.ball = ball;
        this.powerActivation = powerActivation;
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
        GameplayInputButtonState contextQ = inputReader.ReadButton(GameplayInputAction.ContextQ);
        GameplayInputButtonState grab = inputReader.ReadButton(GameplayInputAction.Grab);
        GameplayInputButtonState dodge = inputReader.ReadButton(GameplayInputAction.Dodge);

        if (combat != null && combat.IsGrabRestricted)
        {
            if (combat.IsHoldingGrab && grab.WasPressed)
                combat.TryCancelGrab();
            else if (combat.IsHeldByGrab && dodge.WasPressed)
                combat.TryEscapeGrab(characterActionDirection);
            return;
        }

        bool actuallyHasBall = ball != null && ball.HasBall;
        bool opponentHasBall = !actuallyHasBall && PlayerBallHandler.CurrentOwner != null;
        bool withinAcquireRange = ball != null && ball.IsWithinAcquireRange;
        bool sprintHeld = inputReader.ReadButton(GameplayInputAction.Sprint).IsPressed;
        possessionContext.Update(Time.time, actuallyHasBall, opponentHasBall, withinAcquireRange, sprintHeld);
        bool mouseActionsBlocked = possessionContext.AreMouseActionsBlocked(Time.time);

        if (inputReader.ReadButton(GameplayInputAction.CancelAction).WasPressed)
        {
            if (powerActivation != null && powerActivation.TryCancel())
                return;
            ball?.CancelCharge();
            oneTouchBuffer.Clear();
            ClearChargeInput();
            return;
        }

        if (contextQ.WasPressed)
        {
            bool accepted = combat != null && combat.TryStartDefense();
            powerActivation?.TryConsume(EnhancedActionKind.Defense, accepted);
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

        if (dodge.WasPressed)
            locomotion?.TryDodge(characterActionDirection);

        if (grab.WasPressed)
        {
            if (!actuallyHasBall)
            {
                bool accepted = combat != null && combat.TryGrab(characterActionDirection);
                powerActivation?.TryConsume(EnhancedActionKind.Grab, accepted);
            }
            return;
        }

        if (contextF.WasPressed)
        {
            if (!possessionContext.HasPossessionContext)
            {
                bool accepted = combat != null && combat.TrySlideTackle(characterActionDirection);
                powerActivation?.TryConsume(EnhancedActionKind.SlideTackle, accepted);
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

        if (primary.WasPressed && powerActivation != null && powerActivation.IsArmed)
        {
            bool accepted = actuallyHasBall
                ? !mouseActionsBlocked && ball.TryPowerLobPass(ballAimDirection)
                : combat != null && combat.TryPowerStun(characterActionDirection);
            powerActivation.TryConsume(EnhancedActionKind.Primary, accepted);
            return;
        }

        if (primary.WasPressed)
        {
            if (possessionContext.HasPossessionContext)
            {
                if (!mouseActionsBlocked)
                    powerActivation?.TryConsume(EnhancedActionKind.Primary, BeginChargeInput(BallChargeAction.Pass, ChargeInputButton.Primary));
            }
            else
            {
                bool accepted = combat != null && combat.TryPunch(characterActionDirection);
                powerActivation?.TryConsume(EnhancedActionKind.Primary, accepted);
                possessionContext.BeginCombatProtection(Time.time);
            }
        }
        else if (secondary.WasPressed)
        {
            if (possessionContext.HasPossessionContext)
            {
                if (!mouseActionsBlocked)
                    powerActivation?.TryConsume(EnhancedActionKind.Secondary, BeginChargeInput(BallChargeAction.Shot, ChargeInputButton.Secondary));
            }
            else
            {
                bool accepted = combat != null && combat.TryCrossPunch(characterActionDirection);
                powerActivation?.TryConsume(EnhancedActionKind.Secondary, accepted);
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

    private bool BeginChargeInput(BallChargeAction action, ChargeInputButton button)
    {
        if (ball != null && ball.HasBall)
        {
            ball.StartCharge(action);
            if (ball.IsCharging)
            {
                activeChargeAction = action;
                activeChargeButton = button;
            }
            return ball.IsCharging;
        }

        pendingChargeAction = action;
        pendingChargeButton = button;
        return false;
    }

    private void ClearChargeInput()
    {
        pendingChargeAction = BallChargeAction.None;
        pendingChargeButton = ChargeInputButton.None;
        activeChargeAction = BallChargeAction.None;
        activeChargeButton = ChargeInputButton.None;
    }
}
