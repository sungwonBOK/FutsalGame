### Task 1: Define action names and create the input reader

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs`
- Create: `Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs`

**Interfaces:**
- Produces: `GameplayInputAction` enum values `Move`, `Sprint`, `Pass`, `Shot`, `CancelCharge`, `Dodge`, `Punch`, `SlideTackle`, `Pause`, `Restart`, and `ToggleLegacyCamera`.
- Produces: `GameplayInputButtonState` plus `GameplayInputReader.ReadButton(GameplayInputAction action)`, `ReadMove()`, and `GetBindingDisplayString(GameplayInputAction action)`.
- Consumes: a serialized `InputActionAsset` with a `Player` map.

- [ ] **Step 1: Write the failing reader-contract test**

```csharp
[Test]
public void BindingDisplayString_UsesTheActionOverrideWhenPresent()
{
    InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
    asset.AddActionMap("Player")
        .AddAction("ToggleLegacyCamera", InputActionType.Button)
        .AddBinding("<Keyboard>/f5");
    GameplayInputReader reader = CreateReader(asset);
    asset.FindAction("ToggleLegacyCamera").ApplyBindingOverride("<Keyboard>/f6");

    Assert.That(reader.GetBindingDisplayString(GameplayInputAction.ToggleLegacyCamera), Is.EqualTo("F6"));
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run: Unity EditMode `GameplayInputReaderTests.BindingDisplayString_UsesTheActionOverrideWhenPresent`.

Expected: compile failure because `GameplayInputReader` and `GameplayInputAction` do not exist.

- [ ] **Step 3: Implement the minimal input boundary**

```csharp
public enum GameplayInputAction { Move, Sprint, Pass, Shot, CancelCharge, Dodge, Punch, SlideTackle, Pause, Restart, ToggleLegacyCamera }

public readonly struct GameplayInputButtonState
{
    public bool WasPressed { get; }
    public bool IsPressed { get; }
    public bool WasReleased { get; }
}

public GameplayInputButtonState ReadButton(GameplayInputAction action);
public Vector2 ReadMove();
public string GetBindingDisplayString(GameplayInputAction action);
```

Resolve each enum value through one private action-name map, return neutral states for a missing map/action, and enable/disable only the reader's `Player` map.

- [ ] **Step 4: Run the focused test and verify GREEN**

Run: Unity EditMode `GameplayInputReaderTests`.

Expected: PASS, including neutral-state and display-override assertions.

- [ ] **Step 5: Commit the reader boundary**

```powershell
git add Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
git commit -m "feat: add gameplay input reader"
```

