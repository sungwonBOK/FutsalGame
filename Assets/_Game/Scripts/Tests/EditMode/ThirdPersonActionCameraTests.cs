using NUnit.Framework;
using UnityEngine;

public class ThirdPersonActionCameraTests
{
    [Test]
    public void ThirdPersonCameraMode_PrefersMoveIntentForItsDesiredYaw()
    {
        ThirdPersonActionCameraSettings settings = ScriptableObject.CreateInstance<ThirdPersonActionCameraSettings>();
        try
        {
            CameraContext context = new CameraContext(
                playerPosition: Vector3.zero,
                velocity: Vector3.back * 10f,
                hasMoveIntent: true,
                moveIntent: Vector3.right,
                actionIntent: Vector3.forward,
                targetForward: Vector3.back,
                hasBallTarget: false,
                ballPosition: Vector3.zero,
                currentYaw: 0f,
                deltaTime: 0.1f);

            CameraModeResult result = new ThirdPersonCameraMode().Resolve(context, settings);

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(90f, result.DesiredYaw)), Is.LessThan(0.001f));
            Assert.That(result.LookPoint, Is.EqualTo(Vector3.up * settings.lookAtHeight));
            Assert.That(result.BallHintRequired, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void CameraPlanBuilder_AssemblesResolvedPoseAndFovWithoutChangingThem()
    {
        CameraRigPose cameraPose = new CameraRigPose(
            new Vector3(1f, 2f, 3f),
            Quaternion.Euler(10f, 20f, 0f));
        CameraRigPose followRigPose = new CameraRigPose(
            new Vector3(4f, 5f, 6f),
            Quaternion.Euler(0f, 30f, 0f));

        CameraPlan plan = CameraPlanBuilder.Build(cameraPose, followRigPose, 88f);

        Assert.That(plan.CameraPose.Position, Is.EqualTo(cameraPose.Position));
        Assert.That(plan.CameraPose.Rotation, Is.EqualTo(cameraPose.Rotation));
        Assert.That(plan.FollowRigPose.Position, Is.EqualTo(followRigPose.Position));
        Assert.That(plan.FieldOfView, Is.EqualTo(88f));
    }

    [Test]
    public void UpdateYaw_InsideDeadZone_DoesNotRotate()
    {
        float velocity = 0f;

        float yaw = ThirdPersonActionCamera.UpdateYaw(0f, 4f, ref velocity, 0.016f, 8f, 0.2f, 180f);

        Assert.That(yaw, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void UpdateYaw_ClampsToMaximumRotationSpeed()
    {
        float velocity = 0f;

        float yaw = ThirdPersonActionCamera.UpdateYaw(0f, 90f, ref velocity, 0.1f, 0f, 0.001f, 45f);

        Assert.That(yaw, Is.LessThanOrEqualTo(4.5f + 0.001f));
    }

    [Test]
    public void UpdateYaw_QuickTurnUsesFastRotationForSideTurn()
    {
        float velocity = 0f;

        float yaw = ThirdPersonActionCamera.UpdateYaw(
            currentYaw: 0f,
            desiredYaw: 90f,
            yawVelocity: ref velocity,
            deltaTime: 0.1f,
            deadZone: 0f,
            smoothTime: 0.24f,
            maxRotationSpeed: 90f,
            quickTurnAngle: 75f,
            quickTurnSmoothTime: 0.01f,
            quickTurnMaxRotationSpeed: 720f);

        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, yaw)), Is.GreaterThan(9f));
    }

    [Test]
    public void UpdateYaw_OppositeTurnUsesNormalRotationLimit()
    {
        float velocity = 0f;

        float yaw = ThirdPersonActionCamera.UpdateYaw(
            currentYaw: 0f,
            desiredYaw: 180f,
            yawVelocity: ref velocity,
            deltaTime: 0.1f,
            deadZone: 0f,
            smoothTime: 0.24f,
            maxRotationSpeed: 90f,
            quickTurnAngle: 75f,
            quickTurnSmoothTime: 0.01f,
            quickTurnMaxRotationSpeed: 720f);

        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, yaw)), Is.LessThanOrEqualTo(9f + 0.001f));
    }

    [Test]
    public void ApplyBallAssist_DoesNotFlipWhenBallIsBehindPlayer()
    {
        float yaw = ThirdPersonActionCamera.ApplyBallAssistYaw(
            currentYaw: 0f,
            playerPosition: Vector3.zero,
            ballPosition: Vector3.back * 10f,
            edgeAngle: 35f,
            maxAssistAngle: 120f,
            strength: 0.25f);

        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, yaw)), Is.LessThan(45f));
    }

    [Test]
    public void ApplyBallAssist_WithNoActiveInputDoesNotChaseMovingBall()
    {
        float yaw = ThirdPersonActionCamera.ApplyBallAssistYaw(
            currentYaw: 0f,
            playerPosition: Vector3.zero,
            ballPosition: Vector3.right * 10f,
            edgeAngle: 10f,
            maxAssistAngle: 120f,
            strength: 1f,
            hasActiveMoveInput: false,
            activeMoveYaw: 0f,
            maxActiveInputAssistAngle: 6f);

        Assert.That(yaw, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void SelectHeading_PrefersMoveIntentOverVelocity()
    {
        Vector3 heading = ThirdPersonActionCamera.SelectHeading(
            hasMoveIntent: true,
            moveIntent: Vector3.right,
            actionIntent: Vector3.forward,
            velocity: Vector3.back * 10f,
            targetForward: Vector3.back,
            fallbackYaw: 180f,
            movementPrioritySpeed: 0.5f);

        Assert.That(heading, Is.EqualTo(Vector3.right));
    }

    [Test]
    public void ApplyBallAssist_WithActiveInputCannotOpposeIntent()
    {
        float yaw = ThirdPersonActionCamera.ApplyBallAssistYaw(
            currentYaw: 0f,
            playerPosition: Vector3.zero,
            ballPosition: Vector3.back * 10f,
            edgeAngle: 10f,
            maxAssistAngle: 179f,
            strength: 1f,
            hasActiveMoveInput: true,
            activeMoveYaw: 0f,
            maxActiveInputAssistAngle: 6f);

        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, yaw)), Is.LessThanOrEqualTo(6f + 0.001f));
    }

    [Test]
    public void CalculateTargetFov_ClampsSprintBoost()
    {
        float fov = ThirdPersonActionCamera.CalculateTargetFov(
            baseFov: 85f,
            speed: 100f,
            sprintSpeed: 8f,
            sprintFovBoost: 4f);

        Assert.That(fov, Is.EqualTo(89f).Within(0.001f));
    }

    [Test]
    public void BuildStableLookRotation_KeepsRollAtZero()
    {
        Quaternion rotation = ThirdPersonActionCamera.BuildStableLookRotation(
            cameraPosition: new Vector3(0f, 5f, -6f),
            lookPoint: new Vector3(1f, 1.5f, 2f));

        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, rotation.eulerAngles.z)), Is.LessThan(0.001f));
    }

    [Test]
    public void BuildFollowRigPose_UsesLookPointHeightAndYawOnly()
    {
        CameraRigPose pose = ThirdPersonActionCamera.BuildFollowRigPose(
            playerPosition: new Vector3(1f, 0f, 2f),
            yaw: 35f,
            lookAtHeight: 1.7f);

        Assert.That(pose.Position, Is.EqualTo(new Vector3(1f, 1.7f, 2f)));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(35f, pose.Rotation.eulerAngles.y)), Is.LessThan(0.001f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, pose.Rotation.eulerAngles.x)), Is.LessThan(0.001f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, pose.Rotation.eulerAngles.z)), Is.LessThan(0.001f));
    }

    [Test]
    public void CinemachineBackend_ApplyRigPoseMovesFollowTargetOnly()
    {
        var backendObject = new GameObject("Camera Backend");
        var followTargetObject = new GameObject("Camera Follow Target");

        try
        {
            CinemachineActionCameraBackend backend = backendObject.AddComponent<CinemachineActionCameraBackend>();
            backend.FollowRigTarget = followTargetObject.transform;

            backend.ApplyRigPose(new CameraRigPose(
                new Vector3(2f, 1.5f, -3f),
                Quaternion.Euler(0f, 70f, 0f)));

            Assert.That(followTargetObject.transform.position, Is.EqualTo(new Vector3(2f, 1.5f, -3f)));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(70f, followTargetObject.transform.eulerAngles.y)), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, followTargetObject.transform.eulerAngles.x)), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, followTargetObject.transform.eulerAngles.z)), Is.LessThan(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(backendObject);
            Object.DestroyImmediate(followTargetObject);
        }
    }
}
