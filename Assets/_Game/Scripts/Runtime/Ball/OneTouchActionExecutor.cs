using UnityEngine;

public sealed class OneTouchActionExecutor
{
    public bool TryAttempt(OneTouchIntent intent, PlayerBallHandler ballHandler, Vector3 actionDirection)
    {
        if (intent == OneTouchIntent.None || ballHandler == null)
            return false;

        if (!ballHandler.HasBall)
        {
            ballHandler.PlayOneTouchWhiff();
            return false;
        }

        return ballHandler.TryPerformOneTouch(intent, actionDirection);
    }

    public bool TryExecuteQueued(OneTouchIntentBuffer buffer, PlayerBallHandler ballHandler, Vector3 actionDirection)
    {
        if (buffer == null || !buffer.IsPreparing || ballHandler == null || !ballHandler.HasBall)
            return false;

        if (!ballHandler.TryPerformOneTouch(buffer.Intent, actionDirection))
            return false;

        buffer.Consume();
        return true;
    }
}
