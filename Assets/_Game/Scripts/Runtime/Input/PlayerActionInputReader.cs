using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public readonly struct ActionButtonState
{
    public ActionButtonState(bool wasPressed, bool isPressed, bool wasReleased)
    {
        WasPressed = wasPressed;
        IsPressed = isPressed;
        WasReleased = wasReleased;
    }

    public bool WasPressed { get; }
    public bool IsPressed { get; }
    public bool WasReleased { get; }
}

public static class PlayerActionInputReader
{
    public static ActionButtonState Read(PlayerActionBinding binding)
    {
        return Combine(
            ReadControl(ResolveMouseControl(binding.MouseButton)),
            ReadControl(ResolveKeyboardControl(binding.KeyboardKey)));
    }

    public static ActionButtonState Combine(ActionButtonState first, ActionButtonState second)
    {
        bool isPressed = first.IsPressed || second.IsPressed;
        return new ActionButtonState(
            first.WasPressed || second.WasPressed,
            isPressed,
            !isPressed && (first.WasReleased || second.WasReleased));
    }

    private static ActionButtonState ReadControl(ButtonControl control)
    {
        return control == null
            ? default
            : new ActionButtonState(control.wasPressedThisFrame, control.isPressed, control.wasReleasedThisFrame);
    }

    private static ButtonControl ResolveMouseControl(PlayerMouseButton button)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return null;

        return button switch
        {
            PlayerMouseButton.Left => mouse.leftButton,
            PlayerMouseButton.Right => mouse.rightButton,
            PlayerMouseButton.Middle => mouse.middleButton,
            _ => null
        };
    }

    private static ButtonControl ResolveKeyboardControl(Key key)
    {
        return key == Key.None || Keyboard.current == null ? null : Keyboard.current[key];
    }
}
