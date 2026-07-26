public readonly struct GameplayActionContext
{
    public GameplayActionContext(bool hasPossessionContext, bool mouseActionsBlocked, bool isCharging)
    {
        HasPossessionContext = hasPossessionContext;
        MouseActionsBlocked = mouseActionsBlocked;
        IsCharging = isCharging;
    }

    public bool HasPossessionContext { get; }
    public bool MouseActionsBlocked { get; }
    public bool IsCharging { get; }
}
