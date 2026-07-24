using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CharacterMovementResponsibilityTests
{
    [Test]
    public void MovementConfig_SelectsPossessionProfileBeforeSprint()
    {
        CharacterMovementConfig config = ScriptableObject.CreateInstance<CharacterMovementConfig>();

        try
        {
            CharacterMovementProfile profile = config.ResolveProfile(sprint: true, hasBall: true);

            Assert.That(profile.speed, Is.LessThan(config.Normal.speed));
            Assert.That(profile.rotationSpeed, Is.LessThan(config.Normal.rotationSpeed));
            Assert.That(profile.acceleration, Is.GreaterThan(0f));
            Assert.That(profile.deceleration, Is.GreaterThan(0f));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void Locomotion_OwnsMoveIntentActionDirectionAndProfileSelection()
    {
        GameObject player = new GameObject("Player");

        try
        {
            player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            player.AddComponent<CharacterMotor>();
            CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
            InvokePrivate(locomotion, "Awake");

            locomotion.SetPlayerMoveInput(Vector2.right, Vector3.right, sprint: true, hasBall: false);

            Assert.That(locomotion.HasMoveInput, Is.True);
            Assert.That(locomotion.RawMoveInput, Is.EqualTo(Vector2.right));
            Assert.That(locomotion.MoveDirection, Is.EqualTo(Vector3.right));
            Assert.That(locomotion.ActionDirection, Is.EqualTo(Vector3.right));
            Assert.That(locomotion.ActiveMovementProfile.rotationSpeed, Is.GreaterThan(locomotion.Config.Normal.rotationSpeed));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void Motor_AppliesProvidedProfileWithoutOwningProfileSelection()
    {
        GameObject player = new GameObject("Player");

        try
        {
            Rigidbody body = player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            CharacterMotor motor = player.AddComponent<CharacterMotor>();
            InvokePrivate(motor, "Awake");

            motor.SetMovement(Vector3.forward, new CharacterMovementProfile(4f, 20f, 30f, 360f));
            body.linearVelocity = Vector3.forward * 8f;

            InvokePrivate(motor, "FixedUpdate");

            Assert.That(body.linearVelocity.z, Is.LessThan(8f));
            Assert.That(body.linearVelocity.z, Is.GreaterThan(0f));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void MovementConfig_UsesGameplayDefaultsWhenLegacyAssetHasNoMobilityValues()
    {
        CharacterMovementConfig config = ScriptableObject.CreateInstance<CharacterMovementConfig>();

        try
        {
            SetPrivateField(config, "maxStamina", 0f);
            SetPrivateField(config, "dodgeCost", 0f);
            SetPrivateField(config, "dodgeDuration", 0f);

            Assert.That(config.MaxStamina, Is.EqualTo(100f));
            Assert.That(config.DodgeCost, Is.EqualTo(30f));
            Assert.That(config.DodgeDuration, Is.EqualTo(0.22f));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void Locomotion_DodgeConsumesStaminaAndGrantsTemporaryInvulnerability()
    {
        GameObject player = new GameObject("Player");

        try
        {
            player.AddComponent<Rigidbody>();
            CharacterState state = player.AddComponent<CharacterState>();
            CharacterMotor motor = player.AddComponent<CharacterMotor>();
            CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
            InvokePrivate(motor, "Awake");
            InvokePrivate(locomotion, "Awake");

            bool dodged = locomotion.TryDodge(Vector3.right);

            Assert.That(dodged, Is.True);
            Assert.That(locomotion.IsDodging, Is.True);
            Assert.That(locomotion.Stamina01, Is.LessThan(1f));
            Assert.That(state.IsInvulnerable, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void Locomotion_ResetMobilityStateRestoresStaminaAndEndsDodge()
    {
        GameObject player = new GameObject("Player");

        try
        {
            player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            CharacterMotor motor = player.AddComponent<CharacterMotor>();
            CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
            InvokePrivate(motor, "Awake");
            InvokePrivate(locomotion, "Awake");
            locomotion.TryDodge(Vector3.forward);

            locomotion.ResetMobilityState();

            Assert.That(locomotion.Stamina01, Is.EqualTo(1f));
            Assert.That(locomotion.IsDodging, Is.False);
            Assert.That(locomotion.CanDodge, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }
}
