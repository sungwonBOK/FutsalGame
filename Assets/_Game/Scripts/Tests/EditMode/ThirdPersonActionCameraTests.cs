using NUnit.Framework;
using UnityEngine;

public class ThirdPersonActionCameraTests
{
    [Test]
    public void ManualLook_AppliesMouseSensitivityWithoutChangingCharacterState()
    {
        CameraLookController controller = new CameraLookController();
        controller.Initialize(10f, 5f);

        CameraLookState state = controller.Update(
            new Vector2(3f, -2f),
            yawSensitivity: 2f,
            pitchSensitivity: 4f,
            invertY: false,
            minPitch: -35f,
            maxPitch: 65f);

        Assert.That(state.Yaw, Is.EqualTo(16f).Within(0.001f));
        Assert.That(state.Pitch, Is.EqualTo(-3f).Within(0.001f));
    }

    [Test]
    public void ManualLook_ClampsPitchAndKeepsYawForZeroDelta()
    {
        CameraLookController controller = new CameraLookController();
        controller.Initialize(42f, 60f);

        CameraLookState state = controller.Update(
            Vector2.zero,
            yawSensitivity: 1f,
            pitchSensitivity: 1f,
            invertY: false,
            minPitch: -35f,
            maxPitch: 45f);

        Assert.That(state.Yaw, Is.EqualTo(42f).Within(0.001f));
        Assert.That(state.Pitch, Is.EqualTo(45f).Within(0.001f));
    }

    [Test]
    public void PossessionCameraMode_UsesManualYawForForwardFraming()
    {
        ThirdPersonActionCameraSettings settings = ScriptableObject.CreateInstance<ThirdPersonActionCameraSettings>();
        try
        {
            settings.possessionLookForwardOffset = 0.6f;
            CameraContext context = new CameraContext(
                playerPosition: Vector3.zero,
                velocity: Vector3.zero,
                hasBallTarget: true,
                ballPosition: Vector3.right,
                deltaTime: 0.1f,
                isTargetBallOwner: true);

            CameraModeResult result = new PossessionCameraMode().Resolve(
                context,
                settings,
                new CameraLookState(90f, 0f));

            Assert.That(result.LookPoint.x, Is.EqualTo(settings.possessionLookForwardOffset).Within(0.001f));
            Assert.That(result.LookPoint.z, Is.EqualTo(0f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void FollowRigPose_UsesManualYawWithoutTiltingCinemachineBody()
    {
        CameraRigPose pose = PositionResolver.BuildFollowRigPose(
            Vector3.zero,
            new CameraLookState(90f, 30f),
            1.7f);

        Assert.That(Mathf.DeltaAngle(90f, pose.Rotation.eulerAngles.y), Is.EqualTo(0f).Within(0.01f));
        Assert.That(Mathf.DeltaAngle(0f, pose.Rotation.eulerAngles.x), Is.EqualTo(0f).Within(0.01f));
    }

    [Test]
    public void LookOffset_UsesPitchToMoveOnlyTheAimTarget()
    {
        Vector3 offset = CameraLookOffsetResolver.Resolve(pitch: 30f, maxPitch: 60f, maxVerticalOffset: 2.4f);

        Assert.That(offset.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(offset.y, Is.EqualTo(1.2f).Within(0.001f));
        Assert.That(offset.z, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void CameraDirector_SelectsPossessionProfileWhenTargetOwnsBall()
    {
        ThirdPersonActionCameraSettings settings = ScriptableObject.CreateInstance<ThirdPersonActionCameraSettings>();
        try
        {
            settings.possessionLookForwardOffset = 0.6f;
            settings.possessionDistanceOffset = -0.8f;
            settings.possessionHeightOffset = -0.3f;
            CameraContext context = new CameraContext(
                playerPosition: Vector3.zero,
                velocity: Vector3.zero,
                hasBallTarget: true,
                ballPosition: Vector3.right,
                deltaTime: 0.1f,
                isTargetBallOwner: true);

            CameraModeResult result = new CameraDirector().Resolve(context, settings, new CameraLookState(0f, 0f));

            Assert.That(result.BaseMode, Is.EqualTo(CameraBaseMode.Possession));
            Assert.That(result.LookPoint, Is.EqualTo(new Vector3(0f, settings.lookAtHeight, settings.possessionLookForwardOffset)));
            Assert.That(result.Framing.Distance, Is.EqualTo(settings.distance + settings.possessionDistanceOffset));
            Assert.That(result.Framing.Height, Is.EqualTo(settings.height + settings.possessionHeightOffset));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void CameraDirector_ReturnsToThirdPersonWhenTargetLosesBall()
    {
        ThirdPersonActionCameraSettings settings = ScriptableObject.CreateInstance<ThirdPersonActionCameraSettings>();
        try
        {
            CameraContext context = new CameraContext(
                playerPosition: Vector3.zero,
                velocity: Vector3.zero,
                hasBallTarget: true,
                ballPosition: Vector3.right,
                deltaTime: 0.1f,
                isTargetBallOwner: false);

            CameraModeResult result = new CameraDirector().Resolve(context, settings, new CameraLookState(0f, 0f));

            Assert.That(result.BaseMode, Is.EqualTo(CameraBaseMode.ThirdPerson));
            Assert.That(result.Framing.Distance, Is.EqualTo(settings.distance));
            Assert.That(result.Framing.Height, Is.EqualTo(settings.height));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void CameraContextProvider_UsesBallControllerOwnerAsTargetOwnership()
    {
        GameObject ballObject = new GameObject("Ball");
        GameObject playerObject = new GameObject("Player");
        try
        {
            ballObject.AddComponent<SphereCollider>();
            ballObject.AddComponent<Rigidbody>();
            BallController ball = ballObject.AddComponent<BallController>();
            typeof(BallController)
                .GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(ball, null);
            playerObject.AddComponent<CharacterState>();
            PlayerBallHandler player = playerObject.AddComponent<PlayerBallHandler>();
            CameraContextProvider provider = new CameraContextProvider(
                playerObject.transform,
                playerObject.GetComponent<Rigidbody>(),
                ballObject.transform,
                null);

            Assert.That(ball.TryAcquire(player), Is.True);
            Assert.That(provider.TryGet(0.1f, out CameraContext ownedContext), Is.True);
            Assert.That(ownedContext.IsTargetBallOwner, Is.True);

            ball.ClearOwner();
            Assert.That(provider.TryGet(0.1f, out CameraContext releasedContext), Is.True);
            Assert.That(releasedContext.IsTargetBallOwner, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(ballObject);
        }
    }

    [Test]
    public void ThirdPersonCameraMode_UsesStaticPlayerFraming()
    {
        ThirdPersonActionCameraSettings settings = ScriptableObject.CreateInstance<ThirdPersonActionCameraSettings>();
        try
        {
            CameraContext context = new CameraContext(
                playerPosition: Vector3.zero,
                velocity: Vector3.back * 10f,
                hasBallTarget: false,
                ballPosition: Vector3.zero,
                deltaTime: 0.1f);

            CameraModeResult result = new ThirdPersonCameraMode().Resolve(context, settings, new CameraLookState(90f, 30f));

            Assert.That(result.LookPoint, Is.EqualTo(Vector3.up * settings.lookAtHeight));
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

        CameraFramingProfile framing = new CameraFramingProfile(
            lookAtHeight: 1.7f,
            lookForwardOffset: 0f,
            distance: 6.4f,
            height: 3.5f,
            fovBias: 0f);
        Vector3 aimTargetOffset = Vector3.up * 1.2f;

        CameraPlan plan = CameraPlanBuilder.Build(cameraPose, followRigPose, 88f, framing, aimTargetOffset);

        Assert.That(plan.CameraPose.Position, Is.EqualTo(cameraPose.Position));
        Assert.That(plan.CameraPose.Rotation, Is.EqualTo(cameraPose.Rotation));
        Assert.That(plan.FollowRigPose.Position, Is.EqualTo(followRigPose.Position));
        Assert.That(plan.FieldOfView, Is.EqualTo(88f));
        Assert.That(plan.Framing.Distance, Is.EqualTo(6.4f));
        Assert.That(plan.Framing.Height, Is.EqualTo(3.5f));
        Assert.That(plan.AimTargetOffset, Is.EqualTo(aimTargetOffset));
    }

    [Test]
    public void CalculateTargetFov_ClampsSprintBoost()
    {
        float fov = FovResolver.CalculateTargetFov(
            baseFov: 85f,
            speed: 100f,
            sprintSpeed: 8f,
            sprintFovBoost: 4f);

        Assert.That(fov, Is.EqualTo(89f).Within(0.001f));
    }

    [Test]
    public void BuildStableLookRotation_KeepsRollAtZero()
    {
        Quaternion rotation = PositionResolver.BuildStableLookRotation(
            cameraPosition: new Vector3(0f, 5f, -6f),
            lookPoint: new Vector3(1f, 1.5f, 2f));

        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, rotation.eulerAngles.z)), Is.LessThan(0.001f));
    }

    [Test]
    public void BuildFollowRigPose_UsesManualLookState()
    {
        CameraRigPose pose = PositionResolver.BuildFollowRigPose(
            playerPosition: new Vector3(1f, 0f, 2f),
            look: new CameraLookState(35f, 20f),
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
