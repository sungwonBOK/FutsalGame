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
        config.Pass.force = 3.5f;

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
    public void SprintTouch_ReleasesOnlyAfterConfiguredInterval()
    {
        Assert.That(possession.AcquireInitial(true), Is.True);
        interaction.SetSprintInput(true, Vector3.forward);

        Vector3 ignoredImpulse;
        Assert.That(interaction.TryTick(10f, true, Vector3.forward, out ignoredImpulse), Is.False);
        Assert.That(interaction.TryTick(10.49f, true, Vector3.forward, out ignoredImpulse), Is.False);

        Vector3 impulse;
        Assert.That(interaction.TryTick(10.5f, true, Vector3.forward, out impulse), Is.True);
        Assert.That(impulse, Is.EqualTo(Vector3.forward * config.Dribble.sprintTouchForce));
        Assert.That(ball.CurrentOwner, Is.Null);
    }

    [Test]
    public void Pass_ReleasesWithTheSuppliedActionDirection()
    {
        Assert.That(possession.AcquireInitial(true), Is.True);

        Vector3 impulse;
        Assert.That(interaction.TryPass(10f, Vector3.right, Vector3.forward, out impulse), Is.True);
        Assert.That(impulse, Is.EqualTo(Vector3.right * config.Pass.force));
        Assert.That(ball.CurrentOwner, Is.Null);
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
    }
}
