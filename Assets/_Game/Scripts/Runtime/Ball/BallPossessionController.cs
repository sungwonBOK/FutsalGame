using UnityEngine;

public sealed class BallPossessionController
{
    private readonly PlayerBallHandler owner;
    private readonly BallController ball;
    private readonly BallConfig config;

    private float lastReleaseTime = -999f;
    public float LastAcquireTime { get; private set; } = -999f;

    public bool HasBall => ball != null && ball.HasOwner(owner);
    public bool IsWithinAcquireRange => ball != null && WithinAcquireRange();

    public BallPossessionController(PlayerBallHandler owner, BallController ball, BallConfig config)
    {
        this.owner = owner;
        this.ball = ball;
        this.config = config;
    }

    public bool AcquireInitial(bool startWithBall)
    {
        if (!startWithBall || ball == null || PlayerBallHandler.CurrentOwner != null)
            return false;

        bool acquired = ball.TryAcquire(owner);
        if (acquired) LastAcquireTime = Time.time;
        return acquired;
    }

    public bool TryAcquire(float now, bool canAcquire)
    {
        if (!canAcquire || ball == null || HasBall || PlayerBallHandler.CurrentOwner != null)
            return false;

        if (now - lastReleaseTime < config.Possession.reacquireDelay || !WithinAcquireRange())
            return false;

        bool acquired = ball.TryAcquire(owner);
        if (acquired) LastAcquireTime = now;
        return acquired;
    }

    public bool Release(float now, Vector3 impulse)
    {
        if (ball == null || !ball.Release(owner, impulse))
            return false;

        lastReleaseTime = now;
        return true;
    }

    public void ClearIfOwner()
    {
        if (HasBall)
            ball.ClearOwner();
    }

    private bool WithinAcquireRange()
    {
        Vector3 ownerPosition = owner.transform.position;
        Vector3 ballPosition = ball.transform.position;
        ownerPosition.y = 0f;
        ballPosition.y = 0f;

        float acquireRange = config.Possession.acquireRange;
        return (ownerPosition - ballPosition).sqrMagnitude <= acquireRange * acquireRange;
    }
}
