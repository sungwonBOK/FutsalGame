using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MatchResetTests
{
    private static string GameManagerPath => Path.Combine(
        Application.dataPath,
        "_Game/Scripts/Runtime/Match/GameManager.cs");

    private static string CameraSwitcherPath => Path.Combine(
        Application.dataPath,
        "_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs");

    private static string ViewHintPath => Path.Combine(
        Application.dataPath,
        "_Game/Scripts/Runtime/UI/ViewHintUI.cs");

    [Test]
    public void GameManager_UsesThePauseInputAction()
    {
        Assert.That(
            File.ReadAllText(GameManagerPath),
            Does.Contain("GameplayInputAction.Pause"));
    }

    [Test]
    public void CameraViewSwitcher_UsesTheCameraToggleInputAction()
    {
        Assert.That(
            File.ReadAllText(CameraSwitcherPath),
            Does.Contain("GameplayInputAction.ToggleLegacyCamera"));
    }

    [Test]
    public void CameraViewSwitcher_TogglesExclusiveActionAndLegacyCameraOwnership()
    {
        GameObject target = new GameObject("Camera Target");
        GameObject cameraObject = new GameObject("Camera");
        cameraObject.SetActive(false);

        try
        {
            cameraObject.AddComponent<Camera>();
            ThirdPersonActionCamera actionCamera = cameraObject.AddComponent<ThirdPersonActionCamera>();
            CameraViewSwitcher switcher = cameraObject.AddComponent<CameraViewSwitcher>();
            typeof(CameraViewSwitcher)
                .GetField("target", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(switcher, target.transform);
            cameraObject.SetActive(true);
            InvokePrivate(switcher, "Awake");

            InvokePrivate(switcher, "ToggleCameraOwner");

            Assert.That(actionCamera.enabled, Is.False);
            Assert.That(switcher.IsThirdPerson, Is.True);

            InvokePrivate(switcher, "ToggleCameraOwner");

            Assert.That(actionCamera.enabled, Is.True);
            Assert.That(switcher.IsThirdPerson, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void ViewHintUI_UsesTheCameraToggleBindingDisplay()
    {
        Assert.That(
            File.ReadAllText(ViewHintPath),
            Does.Contain("GetBindingDisplayString"));
    }

    [Test]
    public void ResetCharacter_RestoresMobilityState()
    {
        GameObject managerObject = new GameObject("Game Manager");
        GameObject player = new GameObject("Player");

        try
        {
            GameManager manager = managerObject.AddComponent<GameManager>();
            player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            CharacterMotor motor = player.AddComponent<CharacterMotor>();
            CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
            InvokePrivate(motor, "Awake");
            InvokePrivate(locomotion, "Awake");
            Assert.That(locomotion.TryDodge(Vector3.right), Is.True);

            InvokePrivate(manager, "ResetCharacter", player.transform, Vector3.zero, Quaternion.identity);

            Assert.That(locomotion.Stamina01, Is.EqualTo(1f));
            Assert.That(locomotion.IsDodging, Is.False);
            Assert.That(locomotion.CanDodge, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(managerObject);
        }
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, arguments);
    }
}
