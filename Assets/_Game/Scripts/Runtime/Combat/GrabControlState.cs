public enum GrabRole
{
    None,
    Holding,
    Held
}

public sealed class GrabControlState
{
    private GrabRole role;
    private float cancelAvailableAt;
    private float holdingMovementMultiplier = 1f;

    public bool IsHolding => role == GrabRole.Holding;
    public bool IsHeld => role == GrabRole.Held;
    public bool RestrictsMovement => role != GrabRole.None;
    public float MovementMultiplier => IsHeld ? 0f : IsHolding ? holdingMovementMultiplier : 1f;

    public void BeginHolding(float now, float cancelDelay = 0.5f, float movementMultiplier = 1f)
    {
        role = GrabRole.Holding;
        cancelAvailableAt = now + cancelDelay;
        holdingMovementMultiplier = UnityEngine.Mathf.Clamp01(movementMultiplier);
    }

    public void BeginHeld()
    {
        role = GrabRole.Held;
        cancelAvailableAt = float.PositiveInfinity;
        holdingMovementMultiplier = 0f;
    }

    public bool CanUse(GameplayInputAction action, float now)
    {
        if (role == GrabRole.None)
            return true;

        if (role == GrabRole.Holding)
            return action == GameplayInputAction.Grab && now >= cancelAvailableAt;

        return action == GameplayInputAction.Dodge;
    }

    public void Clear()
    {
        role = GrabRole.None;
        cancelAvailableAt = float.NegativeInfinity;
        holdingMovementMultiplier = 1f;
    }
}
