using NUnit.Framework;
using UnityEngine;

public class ThirdPersonActionCameraTests
{
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
        ThirdPersonActionCamera.CameraRigPose pose = ThirdPersonActionCamera.BuildFollowRigPose(
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

            backend.ApplyRigPose(new ThirdPersonActionCamera.CameraRigPose(
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
