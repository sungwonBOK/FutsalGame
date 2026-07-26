using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class CombatBallConfigTests
{
    [Test]
    public void CombatConfig_ProvidesTackleVelocityFromDistanceAndActiveTime()
    {
        CombatConfig config = ScriptableObject.CreateInstance<CombatConfig>();

        try
        {
            config.Tackle.distance = 4.2f;
            config.Tackle.activeTime = 0.35f;

            Assert.That(config.TackleVelocity, Is.EqualTo(12f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void CombatController_CooldownPropertiesReadConfigValues()
    {
        GameObject player = new GameObject("Combat Player");
        CombatConfig config = ScriptableObject.CreateInstance<CombatConfig>();

        try
        {
            config.Punch.cooldown = 1.7f;
            config.Tackle.cooldown = 4.1f;

            player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            player.AddComponent<CharacterMotor>();
            CombatController combat = player.AddComponent<CombatController>();
            SetPrivateField(combat, "config", config);

            Assert.That(combat.PunchCooldown, Is.EqualTo(1.7f));
            Assert.That(combat.SlideCooldown, Is.EqualTo(4.1f));
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void BallConfig_KeepsPossessionAndShotTuningSeparate()
    {
        BallConfig config = ScriptableObject.CreateInstance<BallConfig>();

        try
        {
            config.Possession.acquireRange = 1.35f;
            config.Possession.ownerMaxDistance = 2.4f;
            config.Shot.shootForce = 7.5f;
            config.Shot.maxShootForce = 14f;

            Assert.That(config.Possession.acquireRange, Is.EqualTo(1.35f));
            Assert.That(config.Possession.ownerMaxDistance, Is.EqualTo(2.4f));
            Assert.That(config.Shot.shootForce, Is.EqualTo(7.5f));
            Assert.That(config.Shot.maxShootForce, Is.EqualTo(14f));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void BallController_UsesConfigOwnerDistance()
    {
        GameObject ballObject = new GameObject("Ball");
        BallConfig config = ScriptableObject.CreateInstance<BallConfig>();

        try
        {
            config.Possession.ownerMaxDistance = 3.6f;

            ballObject.AddComponent<Rigidbody>();
            BallController ball = ballObject.AddComponent<BallController>();
            SetPrivateField(ball, "config", config);

            Assert.That(ball.OwnerMaxDistance, Is.EqualTo(3.6f));
        }
        finally
        {
            Object.DestroyImmediate(ballObject);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void BallConfig_ProvidesNewDribbleAndShotTuningDefaults()
    {
        BallConfig config = ScriptableObject.CreateInstance<BallConfig>();

        try
        {
            Assert.That(config.DribbleMaxFollowLag, Is.EqualTo(0.45f));
            Assert.That(config.ShotLoftPerForce, Is.EqualTo(0.15f));
            Assert.That(config.ShotMomentumInherit, Is.EqualTo(0.5f));
            Assert.That(config.FirstTouchWindow, Is.EqualTo(0.35f));
            Assert.That(config.FirstTouchBonus, Is.EqualTo(1.3f));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void CombatController_RejectsPunchWhileDodging()
    {
        GameObject player = new GameObject("Combat Player");

        try
        {
            player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            CharacterMotor motor = player.AddComponent<CharacterMotor>();
            CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
            CombatController combat = player.AddComponent<CombatController>();
            InvokePrivate(motor, "Awake");
            InvokePrivate(locomotion, "Awake");
            InvokePrivate(combat, "Awake");
            Assert.That(locomotion.TryDodge(Vector3.right), Is.True);

            combat.Punch(Vector3.forward);

            Assert.That(combat.PunchRemaining, Is.EqualTo(0f));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void CombatConfig_DefaultCrossPunchMatchesBasicPunchButUsesDoubleAnimationSpeed()
    {
        CombatConfig config = ScriptableObject.CreateInstance<CombatConfig>();

        try
        {
            Assert.That(config.TryGetAction(CombatActionId.BasicPunch, out CombatActionDefinition basic), Is.True);
            Assert.That(config.TryGetAction(CombatActionId.CrossPunch, out CombatActionDefinition cross), Is.True);
            Assert.That(cross.cooldown, Is.EqualTo(basic.cooldown));
            Assert.That(cross.range, Is.EqualTo(basic.range));
            Assert.That(cross.knockbackForce, Is.EqualTo(basic.knockbackForce));
            Assert.That(cross.animationSpeed, Is.EqualTo(2f));
            Assert.That(cross.releaseBallOnHit, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void CombatActionCooldownTracker_TracksEachActionIndependently()
    {
        CombatActionCooldownTracker tracker = new CombatActionCooldownTracker();

        Assert.That(tracker.TryConsume(CombatActionId.BasicPunch, 10f, 1.2f), Is.True);
        Assert.That(tracker.TryConsume(CombatActionId.CrossPunch, 10f, 1.2f), Is.True);
        Assert.That(tracker.TryConsume(CombatActionId.BasicPunch, 10.1f, 1.2f), Is.False);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
    }
}
