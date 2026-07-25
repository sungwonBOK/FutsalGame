using UnityEngine;

public sealed class ContextualPlayerActionRouter
{
    private readonly CharacterLocomotion locomotion;
    private readonly CombatController combat;
    private readonly PlayerBallHandler ball;
    private readonly OneTouchIntentBuffer oneTouchBuffer = new OneTouchIntentBuffer();
    private readonly OneTouchActionExecutor oneTouchExecutor = new OneTouchActionExecutor();

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

    public void Process(GameplayInputReader inputReader, Vector3 actionDirection)
    {
        if (inputReader == null)
            return;

        if (inputReader.ReadButton(GameplayInputAction.CancelAction).WasPressed)
        {
            ball?.CancelCharge();
            oneTouchBuffer.Clear();
            return;
        }

        if (TryQueueOneTouch(inputReader, GameplayInputAction.QueueOneTouchPass, OneTouchIntent.Pass, actionDirection)
            || TryQueueOneTouch(inputReader, GameplayInputAction.QueueOneTouchShot, OneTouchIntent.Shot, actionDirection))
        {
            return;
        }

        if (oneTouchExecutor.TryExecuteQueued(oneTouchBuffer, ball, actionDirection))
            return;

        if (inputReader.ReadButton(GameplayInputAction.Dodge).WasPressed)
            locomotion?.TryDodge(actionDirection);

        GameplayInputButtonState primary = inputReader.ReadButton(GameplayInputAction.PrimaryAction);
        GameplayInputButtonState secondary = inputReader.ReadButton(GameplayInputAction.SecondaryAction);

        if (ball != null && ball.IsCharging)
        {
            if (primary.WasReleased)
                ball.ReleaseCharge(BallChargeAction.Pass, actionDirection);
            else if (secondary.WasReleased)
                ball.ReleaseCharge(BallChargeAction.Shot, actionDirection);
            return;
        }

        if (primary.WasPressed)
        {
            if (ball != null && ball.HasBall)
                ball.StartCharge(BallChargeAction.Pass);
            else
                combat?.Punch(actionDirection);
        }
        else if (secondary.WasPressed && ball != null && ball.HasBall)
        {
            ball.StartCharge(BallChargeAction.Shot);
        }
    }

    public void ClearPreparedActions()
    {
        oneTouchBuffer.Clear();
        ball?.CancelCharge();
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
}
