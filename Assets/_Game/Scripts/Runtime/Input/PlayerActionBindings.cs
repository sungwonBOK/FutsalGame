using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerMouseButton
{
    None,
    Left,
    Right,
    Middle
}

[Serializable]
public struct PlayerActionBinding
{
    [SerializeField] private PlayerMouseButton mouseButton;
    [SerializeField] private Key keyboardKey;

    public PlayerActionBinding(PlayerMouseButton mouseButton, Key keyboardKey)
    {
        this.mouseButton = mouseButton;
        this.keyboardKey = keyboardKey;
    }

    public PlayerMouseButton MouseButton => mouseButton;
    public Key KeyboardKey => keyboardKey;
    public string KeyboardKeyName => keyboardKey.ToString();
}

[CreateAssetMenu(menuName = "Futsal Brawl/Input/Player Action Bindings")]
public class PlayerActionBindings : ScriptableObject
{
    [SerializeField] private PlayerActionBinding pass = new PlayerActionBinding(PlayerMouseButton.Left, Key.None);
    [SerializeField] private PlayerActionBinding shot = new PlayerActionBinding(PlayerMouseButton.Right, Key.None);
    [SerializeField] private PlayerActionBinding cancel = new PlayerActionBinding(PlayerMouseButton.None, Key.C);

    public PlayerActionBinding Pass => pass;
    public PlayerActionBinding Shot => shot;
    public PlayerActionBinding Cancel => cancel;
}
