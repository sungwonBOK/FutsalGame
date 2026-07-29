using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Futsal Brawl/Combat/Combat Config")]
public class CombatConfig : ScriptableObject
{
    [Serializable]
    public struct PunchSettings
    {
        [Min(0f)] public float prepareTime;
        [Min(0f)] public float activeTime;
        [Min(0f)] public float recoveryTime;
        [Min(0f)] public float range;
        [Min(0f)] public float radius;
        [Min(0f)] public float cooldown;
        [Min(0f)] public float knockbackForce;
        [Min(0f)] public float hitStunTime;

        public PunchSettings(float prepareTime, float activeTime, float recoveryTime, float range, float radius, float cooldown, float knockbackForce, float hitStunTime)
        {
            this.prepareTime = prepareTime;
            this.activeTime = activeTime;
            this.recoveryTime = recoveryTime;
            this.range = range;
            this.radius = radius;
            this.cooldown = cooldown;
            this.knockbackForce = knockbackForce;
            this.hitStunTime = hitStunTime;
        }
    }

    [Serializable]
    public struct TackleSettings
    {
        [Min(0f)] public float prepareTime;
        [Min(0.01f)] public float activeTime;
        [Min(0f)] public float recoveryTime;
        [Min(0f)] public float distance;
        [Min(0f)] public float hitRadius;
        [Min(0f)] public float cooldown;
        [Min(0f)] public float knockbackForce;
        [Min(0f)] public float hitStunTime;
        [Min(0f)] public float ballKnockForce;

        public TackleSettings(float prepareTime, float activeTime, float recoveryTime, float distance, float hitRadius, float cooldown, float knockbackForce, float hitStunTime, float ballKnockForce)
        {
            this.prepareTime = prepareTime;
            this.activeTime = activeTime;
            this.recoveryTime = recoveryTime;
            this.distance = distance;
            this.hitRadius = hitRadius;
            this.cooldown = cooldown;
            this.knockbackForce = knockbackForce;
            this.hitStunTime = hitStunTime;
            this.ballKnockForce = ballKnockForce;
        }
    }

    [Serializable]
    public struct AssistSettings
    {
        [Min(0f)] public float forwardAutoAimRange;
        [Min(0f)] public float forwardAutoAimAngle;
        [Range(0f, 0.35f)] public float forwardAutoAimStrength;

        public AssistSettings(float forwardAutoAimRange, float forwardAutoAimAngle, float forwardAutoAimStrength)
        {
            this.forwardAutoAimRange = forwardAutoAimRange;
            this.forwardAutoAimAngle = forwardAutoAimAngle;
            this.forwardAutoAimStrength = forwardAutoAimStrength;
        }
    }

    [Serializable]
    public struct GrabSettings
    {
        [Min(0f)] public float range;
        [Min(0f)] public float radius;
        [Min(0f)] public float duration;
        [Min(0f)] public float cancelDelay;
        [Range(0f, 1f)] public float holderMovementMultiplier;
        [Min(0f)] public float animationSpeed;

        public GrabSettings(float range, float radius, float duration, float cancelDelay, float holderMovementMultiplier, float animationSpeed)
        {
            this.range = range;
            this.radius = radius;
            this.duration = duration;
            this.cancelDelay = cancelDelay;
            this.holderMovementMultiplier = holderMovementMultiplier;
            this.animationSpeed = animationSpeed;
        }
    }

    [Header("Punch")]
    public PunchSettings Punch = new PunchSettings(0f, 0f, 0f, 1.3f, 0.7f, 1.2f, 8f, 1f);

    [Header("Action Catalog")]
    public CombatActionDefinition[] Actions =
    {
        new CombatActionDefinition(CombatActionId.BasicPunch, 1.2f, 1.3f, 0.7f, 4f, 1f, false, 0f, "Punch", 1f),
        new CombatActionDefinition(CombatActionId.CrossPunch, 1.2f, 1.3f, 0.7f, 8f, 1f, true, 6f, "CrossPunch", 2f)
    };

    [Header("Tackle")]
    public TackleSettings Tackle = new TackleSettings(0f, 0.35f, 0f, 4.2f, 0.8f, 3f, 8f, 1f, 6f);

    [Header("Grab")]
    public GrabSettings Grab = new GrabSettings(1.3f, 0.7f, 1.5f, 0.5f, 0.15f, 1.5f);

    [Header("Assist")]
    public AssistSettings Assist = new AssistSettings(2f, 30f, 0.18f);

    public float TackleVelocity => Tackle.distance / Mathf.Max(0.0001f, Tackle.activeTime);

    public bool TryGetAction(CombatActionId id, out CombatActionDefinition definition)
    {
        if (Actions != null)
        {
            foreach (CombatActionDefinition action in Actions)
            {
                if (action.id == id)
                {
                    definition = action;
                    return true;
                }
            }
        }

        if (id == CombatActionId.CrossPunch)
        {
            definition = new CombatActionDefinition(
                CombatActionId.CrossPunch,
                Punch.cooldown,
                Punch.range,
                Punch.radius,
                Punch.knockbackForce,
                Punch.hitStunTime,
                true,
                Tackle.ballKnockForce,
                "CrossPunch",
                2f);
            return true;
        }

        definition = default;
        return false;
    }
}
