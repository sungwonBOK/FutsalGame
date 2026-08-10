using System;
using UnityEngine;

public enum PowerGaugeGainSource
{
    BasicPunchHit,
    CrossPunchHit,
    SlideTackleHit,
    DefenseSuccess,
    Evade,
    GrabHit
}

[Serializable]
public struct PowerGaugeGainRule
{
    public PowerGaugeGainSource source;
    public bool enabled;
    [Min(0f)] public float amount;

    public PowerGaugeGainRule(PowerGaugeGainSource source, bool enabled, float amount)
    {
        this.source = source;
        this.enabled = enabled;
        this.amount = amount;
    }
}

[CreateAssetMenu(menuName = "Futsal Brawl/Characters/Power Gauge Config")]
public class PowerGaugeConfig : ScriptableObject
{
    [Min(1f)] public float capacity = 100f;
    [Min(0f)] public float passiveGainPerSecond = 1f;
    public PowerGaugeGainRule[] gainRules =
    {
        new PowerGaugeGainRule(PowerGaugeGainSource.BasicPunchHit, true, 10f),
        new PowerGaugeGainRule(PowerGaugeGainSource.CrossPunchHit, true, 15f),
        new PowerGaugeGainRule(PowerGaugeGainSource.SlideTackleHit, true, 15f),
        new PowerGaugeGainRule(PowerGaugeGainSource.DefenseSuccess, true, 10f),
        new PowerGaugeGainRule(PowerGaugeGainSource.Evade, true, 10f),
        new PowerGaugeGainRule(PowerGaugeGainSource.GrabHit, false, 0f)
    };

    public bool TryGetGain(PowerGaugeGainSource source, out float amount)
    {
        if (gainRules != null)
        {
            foreach (PowerGaugeGainRule rule in gainRules)
            {
                if (rule.source != source)
                    continue;

                amount = rule.enabled ? Mathf.Max(0f, rule.amount) : 0f;
                return rule.enabled && amount > 0f;
            }
        }

        amount = 0f;
        return false;
    }
}
