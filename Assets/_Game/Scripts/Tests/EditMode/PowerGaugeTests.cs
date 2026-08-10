using NUnit.Framework;
using UnityEngine;

public class PowerGaugeTests
{
    [Test]
    public void Tick_OnlyFillsDuringAnActiveMatchAndClampsAtCapacity()
    {
        PowerGaugeConfig config = CreateConfig();
        config.capacity = 10f;
        config.passiveGainPerSecond = 2f;
        GameObject player = new GameObject("Player");

        try
        {
            PowerGauge gauge = player.AddComponent<PowerGauge>();
            gauge.Configure(config);

            gauge.Tick(3f, matchActive: false);
            gauge.Tick(7f, matchActive: true);

            Assert.That(gauge.CurrentValue, Is.EqualTo(10f));
            Assert.That(gauge.Value01, Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void Add_OnlyRewardsEnabledSuccessSources()
    {
        PowerGaugeConfig config = CreateConfig();
        config.gainRules = new[]
        {
            new PowerGaugeGainRule(PowerGaugeGainSource.BasicPunchHit, true, 10f),
            new PowerGaugeGainRule(PowerGaugeGainSource.GrabHit, false, 50f)
        };
        GameObject player = new GameObject("Player");

        try
        {
            PowerGauge gauge = player.AddComponent<PowerGauge>();
            gauge.Configure(config);

            Assert.That(gauge.TryAdd(PowerGaugeGainSource.BasicPunchHit), Is.True);
            Assert.That(gauge.TryAdd(PowerGaugeGainSource.GrabHit), Is.False);
            Assert.That(gauge.CurrentValue, Is.EqualTo(10f));
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void ResetGauge_ClearsAccumulatedValueForANewMatch()
    {
        PowerGaugeConfig config = CreateConfig();
        config.gainRules = new[]
        {
            new PowerGaugeGainRule(PowerGaugeGainSource.Evade, true, 10f)
        };
        GameObject player = new GameObject("Player");

        try
        {
            PowerGauge gauge = player.AddComponent<PowerGauge>();
            gauge.Configure(config);
            gauge.TryAdd(PowerGaugeGainSource.Evade);

            gauge.ResetGauge();

            Assert.That(gauge.CurrentValue, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(config);
        }
    }

    private static PowerGaugeConfig CreateConfig()
    {
        return ScriptableObject.CreateInstance<PowerGaugeConfig>();
    }
}
