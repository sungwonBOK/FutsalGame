using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BallInteractionControllerTests
{
    private GameObject ballObject;
    private GameObject playerObject;
    private BallConfig config;
    private BallController ball;
    private BallPossessionController possession;
    private BallInteractionController interaction;

    [SetUp]
    public void SetUp()
    {
        ballObject = new GameObject("Ball");
        ballObject.AddComponent<SphereCollider>();
        ballObject.AddComponent<Rigidbody>();
        ball = ballObject.AddComponent<BallController>();
        InvokePrivateMethod(ball, "Awake");

        playerObject = new GameObject("Player");
        playerObject.AddComponent<CharacterState>();
        PlayerBallHandler owner = playerObject.AddComponent<PlayerBallHandler>();

        config = ScriptableObject.CreateInstance<BallConfig>();
        config.Possession.reacquireDelay = 1f;
        config.Dribble.sprintTouchInterval = 0.5f;
        config.Dribble.sprintTouchForce = 3.5f;
        config.Pass.minChargeForce = 3.5f;
        config.Pass.maxChargeForce = 3.5f;

        possession = new BallPossessionController(owner, ball, config);
        interaction = new BallInteractionController(possession, config);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(playerObject);
        Object.DestroyImmediate(ballObject);
    }

    [Test]
    public void SprintTouch_ReleasesAfterConfiguredIntervalWithPossessionMultiplier()
    {
        Assert.That(possession.AcquireInitial(true), Is.True);
        interaction.SetSprintInput(true, Vector3.forward);

        Vector3 ignoredImpulse;
        Assert.That(interaction.TryTick(10f, true, Vector3.forward, out ignoredImpulse), Is.False);
        Assert.That(interaction.TryTick(10.49f, true, Vector3.forward, out ignoredImpulse), Is.False);

        Vector3 impulse;
        Assert.That(interaction.TryTick(10.5f, true, Vector3.forward, out impulse), Is.True);
        Assert.That(impulse, Is.EqualTo(Vector3.forward * config.Dribble.sprintTouchForce * 2f));
        Assert.That(ball.CurrentOwner, Is.Null);
    }

    [Test]
    public void BurstSprintTouch_AppliesTheAdditionalOnePointFourMultiplier()
    {
        Assert.That(possession.AcquireInitial(true), Is.True);
        interaction.SetSprintInput(true, Vector3.forward, burstSprint: true);

        Vector3 ignoredImpulse;
        Assert.That(interaction.TryTick(10f, true, Vector3.forward, out ignoredImpulse), Is.False);

        Vector3 impulse;
        Assert.That(interaction.TryTick(10.5f, true, Vector3.forward, out impulse), Is.True);
        Assert.That(impulse, Is.EqualTo(Vector3.forward * config.Dribble.sprintTouchForce * 2.8f));
    }

    [Test]
    public void Pass_ReleasesWithTheSuppliedActionDirection()
    {
        Assert.That(possession.AcquireInitial(true), Is.True);

        Vector3 impulse;
        Assert.That(interaction.TryPass(10f, Vector3.right, Vector3.forward, out impulse), Is.True);
        Assert.That(impulse, Is.EqualTo(Vector3.right * config.Pass.minChargeForce));
        Assert.That(ball.CurrentOwner, Is.Null);
    }

    [Test]
    public void ReleaseCharge_UsesLatestDirectionAndPassForceRange()
    {
        Assert.That(possession.AcquireInitial(true), Is.True);
        config.Pass.minChargeForce = 3.5f;
        config.Pass.maxChargeForce = 7f;

        Assert.That(interaction.TryStartCharge(10f, BallChargeAction.Pass), Is.True);

        Vector3 impulse;
        Assert.That(interaction.TryReleaseCharge(11f, BallChargeAction.Pass, Vector3.right, Vector3.forward, out impulse), Is.True);
        Assert.That(impulse, Is.EqualTo(Vector3.right * 7f));
    }

    [Test]
    public void CancelCharge_PreventsLaterMatchingRelease()
    {
        Assert.That(possession.AcquireInitial(true), Is.True);
        Assert.That(interaction.TryStartCharge(10f, BallChargeAction.Shot), Is.True);

        interaction.CancelCharge();

        Vector3 impulse;
        Assert.That(interaction.TryReleaseCharge(11f, BallChargeAction.Shot, Vector3.right, Vector3.forward, out impulse), Is.False);
        Assert.That(impulse, Is.EqualTo(Vector3.zero));
        Assert.That(ball.CurrentOwner, Is.Not.Null);
    }

    [Test]
    public void StartCharge_DoesNotReplaceAnActiveChargeWithAnotherAction()
    {
        Assert.That(possession.AcquireInitial(true), Is.True);

        Assert.That(interaction.TryStartCharge(10f, BallChargeAction.Pass), Is.True);
        Assert.That(interaction.TryStartCharge(10.2f, BallChargeAction.Shot), Is.False);

        Vector3 impulse;
        Assert.That(interaction.TryReleaseCharge(11f, BallChargeAction.Pass, Vector3.forward, Vector3.right, out impulse), Is.True);
        Assert.That(impulse, Is.EqualTo(Vector3.forward * config.Pass.maxChargeForce));
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
    }
}
