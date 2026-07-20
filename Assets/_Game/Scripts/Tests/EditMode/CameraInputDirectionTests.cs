using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class CameraInputDirectionTests
{
    [Test]
    public void BuildMoveInput_CombinesWasdAndArrowsThenClampsDiagonal()
    {
        Vector2 input = PlayerInput.BuildMoveInput(
            leftPressed: true,
            rightPressed: false,
            downPressed: false,
            upPressed: true);

        Assert.That(input.x, Is.LessThan(0f));
        Assert.That(input.y, Is.GreaterThan(0f));
        Assert.That(input.magnitude, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void BuildPlanarMoveDirection_ConvertsVector2ToImmediatePlanarIntent()
    {
        Vector3 direction = CharacterMovementUtility.BuildPlanarMoveDirection(new Vector2(1f, 1f));

        Assert.That(direction.y, Is.EqualTo(0f));
        Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.001f));
        Assert.That(direction.x, Is.GreaterThan(0f));
        Assert.That(direction.z, Is.GreaterThan(0f));
    }

    [Test]
    public void BuildCameraRelativeMoveDirection_UsesReferenceYawForLeftAndRight()
    {
        GameObject reference = new GameObject("Camera Reference");

        try
        {
            reference.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            Vector3 right = PlayerInput.BuildCameraRelativeMoveDirection(Vector2.right, reference.transform);
            Vector3 left = PlayerInput.BuildCameraRelativeMoveDirection(Vector2.left, reference.transform);

            Assert.That(right.y, Is.EqualTo(0f));
            Assert.That(left.y, Is.EqualTo(0f));
            Assert.That(Vector3.Dot(right, reference.transform.right), Is.GreaterThan(0.99f));
            Assert.That(Vector3.Dot(left, -reference.transform.right), Is.GreaterThan(0.99f));
        }
        finally
        {
            Object.DestroyImmediate(reference);
        }
    }

    [Test]
    public void SetPlayerMoveInput_UpdatesMoveAndActionDirectionImmediately()
    {
        GameObject player = new GameObject("Player");

        try
        {
            player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            player.AddComponent<CharacterMotor>();
            CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
            InvokePrivate(locomotion, "Awake");

            locomotion.SetPlayerMoveInput(Vector2.right, sprint: true, hasBall: false);

            Assert.That(locomotion.HasMoveInput, Is.True);
            Assert.That(locomotion.MoveDirection, Is.EqualTo(Vector3.right));
            Assert.That(locomotion.ActionDirection, Is.EqualTo(Vector3.right));
            Assert.That(locomotion.ActiveMovementProfile.rotationSpeed, Is.GreaterThan(720f));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void SetPlayerMoveInput_UsesPossessionProfileWhenHoldingBall()
    {
        GameObject player = new GameObject("Player");

        try
        {
            player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            player.AddComponent<CharacterMotor>();
            CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
            InvokePrivate(locomotion, "Awake");

            locomotion.SetPlayerMoveInput(Vector2.up, sprint: true, hasBall: true);

            Assert.That(locomotion.ActiveMovementProfile.speed, Is.LessThan(6f));
            Assert.That(locomotion.ActiveMovementProfile.rotationSpeed, Is.LessThan(720f));
            Assert.That(locomotion.ActiveMovementProfile.acceleration, Is.GreaterThan(0f));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void FixedUpdate_ClearsUncontrolledAngularVelocityWhenIdle()
    {
        GameObject player = new GameObject("Player");

        try
        {
            Rigidbody body = player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            CharacterMotor motor = player.AddComponent<CharacterMotor>();
            InvokePrivate(motor, "Awake");

            motor.SetMovement(Vector3.zero, new CharacterMovementProfile(6f, 45f, 60f, 720f));
            body.angularVelocity = Vector3.up * 12f;

            InvokePrivate(motor, "FixedUpdate");

            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void ResolveActionDirection_FallsBackToCharacterForwardWithoutInput()
    {
        Vector3 direction = CharacterMovementUtility.ResolveActionDirection(
            hasMoveInput: false,
            moveDirection: Vector3.zero,
            characterForward: Vector3.left);

        Assert.That(direction, Is.EqualTo(Vector3.left));
    }

    [Test]
    public void CorrectActionDirectionTowardTarget_OnlyWeaklyAdjustsForwardOpponent()
    {
        Vector3 corrected = CombatController.CorrectActionDirectionTowardTarget(
            origin: Vector3.zero,
            intendedDirection: Vector3.forward,
            targetPosition: new Vector3(0.5f, 0f, 2f),
            maxAngle: 30f,
            strength: 0.2f);

        float correctionAngle = Mathf.Abs(Vector3.SignedAngle(Vector3.forward, corrected, Vector3.up));
        Assert.That(correctionAngle, Is.GreaterThan(0f));
        Assert.That(correctionAngle, Is.LessThan(8f));
    }

    [Test]
    public void CaptureShotDirection_KeepsPressDirectionForRelease()
    {
        Vector3 captured = PlayerBallHandler.CaptureShotDirection(Vector3.right, Vector3.forward);

        Assert.That(captured, Is.EqualTo(Vector3.right));
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
    }
}
