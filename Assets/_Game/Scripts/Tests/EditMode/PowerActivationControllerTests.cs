using NUnit.Framework;
using UnityEngine;

public class PowerActivationControllerTests
{
    [Test]
    public void AcceptedEnhancedAction_ConsumesTheFullGaugeButRejectedActionDoesNot()
    {
        PowerGaugeConfig config = CreateConfig();
        GameObject player = new GameObject("Player");

        try
        {
            PowerGauge gauge = player.AddComponent<PowerGauge>();
            gauge.Configure(config);
            gauge.Tick(1f, matchActive: true);
            PowerActivationController activation = player.AddComponent<PowerActivationController>();

            Assert.That(activation.TryArm(), Is.True);
            Assert.That(activation.TryConsume(EnhancedActionKind.Primary, wasAccepted: false), Is.False);
            Assert.That(gauge.CurrentValue, Is.EqualTo(config.capacity));
            Assert.That(activation.IsArmed, Is.True);

            Assert.That(activation.TryConsume(EnhancedActionKind.Primary, wasAccepted: true), Is.True);
            Assert.That(gauge.CurrentValue, Is.Zero);
            Assert.That(activation.IsArmed, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void Cancel_PreservesTheFullGauge()
    {
        PowerGaugeConfig config = CreateConfig();
        GameObject player = new GameObject("Player");

        try
        {
            PowerGauge gauge = player.AddComponent<PowerGauge>();
            gauge.Configure(config);
            gauge.Tick(1f, matchActive: true);
            PowerActivationController activation = player.AddComponent<PowerActivationController>();

            activation.TryArm();

            Assert.That(activation.TryCancel(), Is.True);
            Assert.That(gauge.CurrentValue, Is.EqualTo(config.capacity));
            Assert.That(activation.IsArmed, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void ResetForNewMatch_ClearsBothTheGaugeAndArmedState()
    {
        PowerGaugeConfig config = CreateConfig();
        GameObject player = new GameObject("Player");

        try
        {
            PowerGauge gauge = player.AddComponent<PowerGauge>();
            gauge.Configure(config);
            gauge.Tick(1f, matchActive: true);
            PowerActivationController activation = player.AddComponent<PowerActivationController>();
            activation.TryArm();

            activation.ResetForNewMatch();

            Assert.That(gauge.CurrentValue, Is.Zero);
            Assert.That(activation.IsArmed, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(config);
        }
    }

    private static PowerGaugeConfig CreateConfig()
    {
        PowerGaugeConfig config = ScriptableObject.CreateInstance<PowerGaugeConfig>();
        config.capacity = 10f;
        config.passiveGainPerSecond = 10f;
        return config;
    }
}
