# Mouse Look Camera Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the mouse rotate only the third-person camera while retaining camera-relative keyboard movement and character rotation only during movement.

**Architecture:** A raw input helper reads mouse delta, and a focused controller turns it into bounded yaw/pitch state. Camera modes select framing only; `PositionResolver` consumes the manual view state to produce direct-camera and Cinemachine rig poses.

**Tech Stack:** Unity 6000.5, Unity Input System, Cinemachine 3.1.7, NUnit EditMode tests.

## Global Constraints

- Work only in camera issue #2; do not modify `PlayerInput.cs` because it already creates camera-relative WASD movement.
- Preserve pre-existing uncommitted possession-framing work.
- Do not directly edit `.unity`, `.prefab`, `.asset`, or `.inputactions` YAML.
- Keep each new file limited to one responsibility.
- Do not remove `CameraViewSwitcher` or `ViewHintUI` in this change; their UI dependency requires a separate approved cleanup task.

---

### Task 1: Add testable manual-look state

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Camera/Look/CameraLookState.cs`
- Create: `Assets/_Game/Scripts/Runtime/Camera/Look/CameraLookController.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/ThirdPersonActionCameraTests.cs`

**Interfaces:**
- Produces `CameraLookState(float yaw, float pitch)` with `Yaw` and `Pitch` getters.
- Produces `CameraLookController.Initialize(float yaw, float pitch)` and `Update(Vector2 mouseDelta, float yawSensitivity, float pitchSensitivity, bool invertY, float minPitch, float maxPitch)`.

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void ManualLook_AppliesMouseSensitivityWithoutChangingPitchSign()
{
    CameraLookController controller = new CameraLookController();
    controller.Initialize(10f, 5f);

    CameraLookState state = controller.Update(new Vector2(3f, -2f), 2f, 4f, false, -35f, 65f);

    Assert.That(state.Yaw, Is.EqualTo(16f).Within(0.001f));
    Assert.That(state.Pitch, Is.EqualTo(-3f).Within(0.001f));
}

[Test]
public void ManualLook_ClampsPitchAndKeepsYawForZeroDelta()
{
    CameraLookController controller = new CameraLookController();
    controller.Initialize(42f, 60f);

    CameraLookState state = controller.Update(Vector2.zero, 1f, 1f, false, -35f, 45f);

    Assert.That(state.Yaw, Is.EqualTo(42f).Within(0.001f));
    Assert.That(state.Pitch, Is.EqualTo(45f).Within(0.001f));
}
```

- [ ] **Step 2: Run the named EditMode tests and verify they fail because the manual-look API is missing.**

- [ ] **Step 3: Add the two focused types.**

```csharp
public readonly struct CameraLookState
{
    public CameraLookState(float yaw, float pitch) { Yaw = yaw; Pitch = pitch; }
    public float Yaw { get; }
    public float Pitch { get; }
}
```

```csharp
public sealed class CameraLookController
{
    private float yaw;
    private float pitch;
    public void Initialize(float initialYaw, float initialPitch) { yaw = initialYaw; pitch = initialPitch; }
    public CameraLookState Update(Vector2 delta, float yawSensitivity, float pitchSensitivity, bool invertY, float minPitch, float maxPitch)
    {
        yaw += delta.x * Mathf.Max(0f, yawSensitivity);
        float pitchDirection = invertY ? -1f : 1f;
        pitch += delta.y * Mathf.Max(0f, pitchSensitivity) * pitchDirection;
        pitch = Mathf.Clamp(pitch, Mathf.Min(minPitch, maxPitch), Mathf.Max(minPitch, maxPitch));
        return new CameraLookState(yaw, pitch);
    }
}
```

- [ ] **Step 4: Run the named tests and verify they pass.**

### Task 2: Make camera modes framing-only

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Camera/Core/CameraContext.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Camera/Core/CameraContextProvider.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Camera/Core/CameraModeResult.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Camera/Modes/ThirdPersonCameraMode.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Camera/Modes/PossessionCameraMode.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/ThirdPersonActionCameraTests.cs`

**Interfaces:**
- `CameraContext` retains player position, velocity, ball state, delta time, and current camera position; it removes movement-heading and automatic-yaw data.
- `CameraModeResult` retains `BaseMode`, `LookPoint`, and `Framing`; it removes `DesiredYaw` and `BallHintRequired`.

- [ ] **Step 1: Write a failing test proving possession look-forward framing uses the provided manual yaw rather than movement state.**

```csharp
CameraLookState look = new CameraLookState(90f, 0f);
CameraModeResult result = new PossessionCameraMode().Resolve(context, settings, look);
Assert.That(result.LookPoint.x, Is.GreaterThan(context.PlayerPosition.x));
```

- [ ] **Step 2: Run the test and verify it fails because the mode has no manual-look parameter.**

- [ ] **Step 3: Pass `CameraLookState` to both modes and build possession look-forward offset from `look.Yaw`; delete heading selection, ball-assist yaw, and ball-hint outputs.**

- [ ] **Step 4: Run the named test and all camera EditMode tests.**

### Task 3: Apply manual look to camera pose and backend plan

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Input/MouseLookInput.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Camera/Resolvers/PositionResolver.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Camera/ThirdPersonActionCamera.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Camera/ThirdPersonActionCameraSettings.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/ThirdPersonActionCameraTests.cs`

**Interfaces:**
- `MouseLookInput.ReadDelta()` returns `Mouse.current.delta.ReadValue()` or `Vector2.zero` with no mouse.
- `PositionResolver.Resolve` accepts `CameraLookState` and uses its yaw/pitch for direct pose and follow-rig pose.
- Settings add `mouseYawSensitivity`, `mousePitchSensitivity`, `invertMouseY`, `minPitch`, and `maxPitch`.

- [ ] **Step 1: Write failing tests for pitch clamping in `CameraLookController` and a follow-rig pose that contains both supplied yaw and pitch.**

```csharp
CameraRigPose pose = PositionResolver.BuildFollowRigPose(Vector3.zero, new CameraLookState(90f, 30f), 1.7f);
Assert.That(Mathf.DeltaAngle(90f, pose.Rotation.eulerAngles.y), Is.EqualTo(0f).Within(0.01f));
Assert.That(Mathf.DeltaAngle(30f, pose.Rotation.eulerAngles.x), Is.EqualTo(0f).Within(0.01f));
```

- [ ] **Step 2: Run the named tests and verify they fail for the new pose API.**

- [ ] **Step 3: Implement the minimum integration.**

```csharp
CameraLookState look = lookController.Update(
    MouseLookInput.ReadDelta(), settings.mouseYawSensitivity, settings.mousePitchSensitivity,
    settings.invertMouseY, settings.minPitch, settings.maxPitch);
CameraModeResult mode = cameraDirector.Resolve(context, settings, look);
CameraPositionResult position = positionResolver.Resolve(mode, look, context, settings, cameraBackend.UsesCinemachineBackend);
```

- [ ] **Step 4: Replace automatic-yaw-only tests with manual-look tests; run the complete camera EditMode suite.**

### Task 4: Remove obsolete auto-yaw code and document the runtime structure

**Files:**
- Delete: `Assets/_Game/Scripts/Runtime/Camera/Resolvers/AimResolver.cs`
- Delete: `Assets/_Game/Scripts/Runtime/Camera/Resolvers/AimResolver.cs.meta`
- Modify: `Assets/_Game/Scripts/Runtime/Camera/ThirdPersonActionCamera.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/ThirdPersonActionCameraTests.cs`
- Modify: `PROJECT_STRUCTURE.md`
- Modify: `IMPLEMENTATION_STATUS.md`

- [ ] **Step 1: Confirm `rg` shows that only obsolete tests and compatibility methods reference `AimResolver`.**
- [ ] **Step 2: Delete the obsolete tests and `ThirdPersonActionCamera` yaw compatibility methods, then delete `AimResolver` through the Unity-safe asset path.**
- [ ] **Step 3: Update project docs with the Input/Camera Look boundary and the confirmed no-auto-yaw rule.**
- [ ] **Step 4: Run full EditMode tests, inspect the Unity Console for compile errors, review `git diff --check`, and perform Play Mode control verification.**
