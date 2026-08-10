public enum GameplayInputAction
{
    Move,
    Sprint,
    PrimaryAction,
    SecondaryAction,
    QueueOneTouchPass,
    QueueOneTouchShot,
    CancelAction,
    PowerActivation,
    ContextQ,
    Grab,
    ContextF,
    Dodge,
    Pause,
    ToggleLegacyCamera
}

public readonly struct GameplayInputButtonState
{
    public GameplayInputButtonState(bool wasPressed, bool isPressed, bool wasReleased)
    {
        WasPressed = wasPressed;
        IsPressed = isPressed;
        WasReleased = wasReleased;
    }

    public bool WasPressed { get; }
    public bool IsPressed { get; }
    public bool WasReleased { get; }
}
