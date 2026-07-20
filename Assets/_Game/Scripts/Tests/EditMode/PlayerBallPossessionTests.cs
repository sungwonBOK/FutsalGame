using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class PlayerBallPossessionTests
{
    private GameObject ballObject;
    private GameObject playerObject;
    private BallConfig config;
    private BallController ball;
    private PlayerBallHandler owner;

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
        owner = playerObject.AddComponent<PlayerBallHandler>();

        config = ScriptableObject.CreateInstance<BallConfig>();
        config.Possession.acquireRange = 2f;
        config.Possession.reacquireDelay = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(playerObject);
        Object.DestroyImmediate(ballObject);
    }

    [Test]
    public void Release_PreventsReacquisitionUntilConfiguredDelayExpires()
    {
        BallPossessionController possession = new BallPossessionController(owner, ball, config);
        Assert.That(possession.AcquireInitial(true), Is.True);

        Assert.That(possession.Release(10f, Vector3.forward), Is.True);
        Assert.That(possession.TryAcquire(10.5f, true), Is.False);
        Assert.That(possession.TryAcquire(11f, true), Is.True);
    }

    [Test]
    public void ClearIfOwner_ClearsOnlyTheOwningPlayersPossession()
    {
        BallPossessionController possession = new BallPossessionController(owner, ball, config);
        Assert.That(possession.AcquireInitial(true), Is.True);

        possession.ClearIfOwner();

        Assert.That(ball.CurrentOwner, Is.Null);
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
    }
}
