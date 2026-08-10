public enum EnhancedActionKind
{
    None,
    Primary,
    Secondary,
    Defense,
    Grab,
    SlideTackle,
    BurstSprint
}

public sealed class PowerActivationState
{
    public bool IsArmed { get; private set; }

    public bool TryArm(bool isGaugeFull)
    {
        if (!isGaugeFull || IsArmed)
            return false;

        IsArmed = true;
        return true;
    }

    public bool TryCancel()
    {
        if (!IsArmed)
            return false;

        IsArmed = false;
        return true;
    }

    public bool TryConsume(EnhancedActionKind action, bool wasAccepted)
    {
        if (!IsArmed || action == EnhancedActionKind.None || !wasAccepted)
            return false;

        IsArmed = false;
        return true;
    }

    public void Reset()
    {
        IsArmed = false;
    }
}
