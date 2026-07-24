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
            { GameplayInputAction.Pass, "Pass" },
            { GameplayInputAction.Shot, "Shot" },
            { GameplayInputAction.CancelCharge, "CancelCharge" },
            { GameplayInputAction.Dodge, "Dodge" },
            { GameplayInputAction.Punch, "Punch" },
            { GameplayInputAction.SlideTackle, "SlideTackle" },
            { GameplayInputAction.Pause, "Pause" },
            { GameplayInputAction.Restart, "Restart" },
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
        InputAction inputAction = ResolveAction(action);
        return inputAction == null
            ? default
            : new GameplayInputButtonState(
                inputAction.WasPressedThisFrame(),
                inputAction.IsPressed(),
                inputAction.WasReleasedThisFrame());
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
}
