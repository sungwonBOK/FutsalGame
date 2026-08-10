using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PowerGaugeIntegrationTests
{
    [Test]
    public void Punch_OnlyRewardsTheAttackerAfterAnActualHit()
    {
        PowerGaugeConfig config = CreateConfig(PowerGaugeGainSource.BasicPunchHit, 10f);
        GameObject attacker = new GameObject("Attacker");
        GameObject victim = new GameObject("Victim");

        try
        {
            victim.transform.position = Vector3.forward;
            victim.AddComponent<Rigidbody>();
            victim.AddComponent<SphereCollider>();
            CharacterState victimState = victim.AddComponent<CharacterState>();
            InvokePrivate(victimState, "Awake");

            attacker.AddComponent<Rigidbody>();
            CharacterState attackerState = attacker.AddComponent<CharacterState>();
            CharacterMotor motor = attacker.AddComponent<CharacterMotor>();
            PowerGauge gauge = attacker.AddComponent<PowerGauge>();
            CombatController combat = attacker.AddComponent<CombatController>();
            gauge.Configure(config);
            InvokePrivate(attackerState, "Awake");
            InvokePrivate(motor, "Awake");
            InvokePrivate(combat, "Awake");
            Physics.SyncTransforms();

            combat.Punch(Vector3.forward);

            Assert.That(gauge.CurrentValue, Is.EqualTo(10f));
        }
        finally
        {
            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(victim);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void PunchWithoutATarget_DoesNotRewardTheAttacker()
    {
        PowerGaugeConfig config = CreateConfig(PowerGaugeGainSource.BasicPunchHit, 10f);
        GameObject attacker = new GameObject("Attacker");

        try
        {
            attacker.AddComponent<Rigidbody>();
            CharacterState attackerState = attacker.AddComponent<CharacterState>();
            CharacterMotor motor = attacker.AddComponent<CharacterMotor>();
            PowerGauge gauge = attacker.AddComponent<PowerGauge>();
            CombatController combat = attacker.AddComponent<CombatController>();
            gauge.Configure(config);
            InvokePrivate(attackerState, "Awake");
            InvokePrivate(motor, "Awake");
            InvokePrivate(combat, "Awake");

            combat.Punch(Vector3.forward);

            Assert.That(gauge.CurrentValue, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void CrossPunch_OnlyRewardsTheAttackerAfterAnActualHit()
    {
        PowerGaugeConfig config = CreateConfig(PowerGaugeGainSource.CrossPunchHit, 15f);
        GameObject attacker = new GameObject("Attacker");
        GameObject victim = new GameObject("Victim");

        try
        {
            victim.transform.position = Vector3.forward;
            victim.AddComponent<Rigidbody>();
            victim.AddComponent<SphereCollider>();
            CharacterState victimState = victim.AddComponent<CharacterState>();
            InvokePrivate(victimState, "Awake");

            attacker.AddComponent<Rigidbody>();
            CharacterState attackerState = attacker.AddComponent<CharacterState>();
            CharacterMotor motor = attacker.AddComponent<CharacterMotor>();
            PowerGauge gauge = attacker.AddComponent<PowerGauge>();
            CombatController combat = attacker.AddComponent<CombatController>();
            gauge.Configure(config);
            InvokePrivate(attackerState, "Awake");
            InvokePrivate(motor, "Awake");
            InvokePrivate(combat, "Awake");
            Physics.SyncTransforms();

            combat.CrossPunch(Vector3.forward);

            Assert.That(gauge.CurrentValue, Is.EqualTo(15f));
        }
        finally
        {
            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(victim);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void SlideTackle_OnlyRewardsTheAttackerAfterAnActualHit()
    {
        PowerGaugeConfig config = CreateConfig(PowerGaugeGainSource.SlideTackleHit, 15f);
        GameObject attacker = new GameObject("Attacker");
        GameObject victim = new GameObject("Victim");

        try
        {
            victim.transform.position = Vector3.forward * 0.5f;
            victim.AddComponent<Rigidbody>();
            victim.AddComponent<SphereCollider>();
            CharacterState victimState = victim.AddComponent<CharacterState>();
            InvokePrivate(victimState, "Awake");

            attacker.AddComponent<Rigidbody>();
            CharacterState attackerState = attacker.AddComponent<CharacterState>();
            CharacterMotor motor = attacker.AddComponent<CharacterMotor>();
            PowerGauge gauge = attacker.AddComponent<PowerGauge>();
            CombatController combat = attacker.AddComponent<CombatController>();
            gauge.Configure(config);
            InvokePrivate(attackerState, "Awake");
            InvokePrivate(motor, "Awake");
            InvokePrivate(combat, "Awake");
            Physics.SyncTransforms();

            combat.SlideTackle(Vector3.forward);
            InvokePrivate(combat, "FixedUpdate");

            Assert.That(gauge.CurrentValue, Is.EqualTo(15f));
        }
        finally
        {
            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(victim);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void NotifyEvaded_RewardsTheCharacterThatActuallyEvaded()
    {
        PowerGaugeConfig config = CreateConfig(PowerGaugeGainSource.Evade, 10f);
        GameObject player = new GameObject("Player");

        try
        {
            player.AddComponent<Rigidbody>();
            CharacterState state = player.AddComponent<CharacterState>();
            PowerGauge gauge = player.AddComponent<PowerGauge>();
            gauge.Configure(config);
            InvokePrivate(state, "Awake");

            state.NotifyEvaded();

            Assert.That(gauge.CurrentValue, Is.EqualTo(10f));
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void SuccessfulDefense_RewardsTheDefenderOnce()
    {
        PowerGaugeConfig config = CreateConfig(PowerGaugeGainSource.DefenseSuccess, 10f);
        GameObject player = new GameObject("Player");

        try
        {
            player.AddComponent<Rigidbody>();
            player.AddComponent<CharacterState>();
            CharacterMotor motor = player.AddComponent<CharacterMotor>();
            CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
            PowerGauge gauge = player.AddComponent<PowerGauge>();
            DefenseController defense = player.AddComponent<DefenseController>();
            gauge.Configure(config);
            InvokePrivate(motor, "Awake");
            InvokePrivate(locomotion, "Awake");
            InvokePrivate(defense, "Awake");

            Assert.That(defense.TryStartDefense(), Is.True);
            Assert.That(defense.TryBlockAttack(Vector3.forward), Is.True);
            Assert.That(gauge.CurrentValue, Is.EqualTo(10f));
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(config);
        }
    }

    private static PowerGaugeConfig CreateConfig(PowerGaugeGainSource source, float amount)
    {
        PowerGaugeConfig config = ScriptableObject.CreateInstance<PowerGaugeConfig>();
        config.gainRules = new[] { new PowerGaugeGainRule(source, true, amount) };
        return config;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
    }
}
