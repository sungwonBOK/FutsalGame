using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameplayInputReader : MonoBehaviour
{
    private static readonly IReadOnlyDictionary<GameplayInputAction, string> ActionNames =
        new Dictionary<GameplayInputAction, string>
        {
            { GameplayInputAction.Move, "Move" },
            { GameplayInputAction.Sprint, "Sprint" },
            { GameplayInputAction.PrimaryAction, "PrimaryAction" },
            { GameplayInputAction.SecondaryAction, "SecondaryAction" },
            { GameplayInputAction.QueueOneTouchPass, "QueueOneTouchPass" },
            { GameplayInputAction.QueueOneTouchShot, "QueueOneTouchShot" },
            { GameplayInputAction.CancelAction, "CancelAction" },
            { GameplayInputAction.PowerActivation, "PowerActivation" },
            { GameplayInputAction.ContextQ, "ContextQ" },
            { GameplayInputAction.Grab, "Grab" },
            { GameplayInputAction.ContextF, "ContextF" },
            { GameplayInputAction.Dodge, "Dodge" },
            { GameplayInputAction.Pause, "Pause" },
            { GameplayInputAction.ToggleLegacyCamera, "ToggleLegacyCamera" }
        };

    [SerializeField] private InputActionAsset inputActions;

    private InputActionMap playerMap;

    private void OnEnable()
    {
        playerMap = inputActions != null ? inputActions.FindActionMap("Player", throwIfNotFound: false) : null;
        playerMap?.Enable();
    }

    private void OnDisable()
    {
        playerMap?.Disable();
    }

    public GameplayInputButtonState ReadButton(GameplayInputAction action)
    {
        GameplayInputButtonState state = ReadRawButton(action);

        if (action == GameplayInputAction.PrimaryAction
            && IsQueueActionActive(GameplayInputAction.QueueOneTouchPass))
        {
            return default;
        }

        if (action == GameplayInputAction.SecondaryAction
            && IsQueueActionActive(GameplayInputAction.QueueOneTouchShot))
        {
            return default;
        }

        return state;
    }

    public Vector2 ReadMove()
    {
        InputAction inputAction = ResolveAction(GameplayInputAction.Move);
        return inputAction != null ? inputAction.ReadValue<Vector2>() : Vector2.zero;
    }

    public string GetBindingDisplayString(GameplayInputAction action)
    {
        InputAction inputAction = ResolveAction(action);
        return inputAction != null ? inputAction.GetBindingDisplayString() : string.Empty;
    }

    private InputAction ResolveAction(GameplayInputAction action)
    {
        if (playerMap == null || !ActionNames.TryGetValue(action, out string actionName))
            return null;

        return playerMap.FindAction(actionName, throwIfNotFound: false);
    }

    private GameplayInputButtonState ReadRawButton(GameplayInputAction action)
    {
        InputAction inputAction = ResolveAction(action);
        return inputAction == null
            ? default
            : new GameplayInputButtonState(
                inputAction.WasPressedThisFrame(),
                inputAction.IsPressed(),
                inputAction.WasReleasedThisFrame());
    }

    private bool IsQueueActionActive(GameplayInputAction action)
    {
        GameplayInputButtonState state = ReadRawButton(action);
        return state.WasPressed || state.IsPressed || state.WasReleased;
    }
}
