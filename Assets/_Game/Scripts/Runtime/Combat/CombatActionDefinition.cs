using System;
using UnityEngine;

[Serializable]
public struct CombatActionDefinition
{
    public CombatActionId id;
    [Min(0f)] public float cooldown;
    [Min(0f)] public float range;
    [Min(0f)] public float radius;
    [Min(0f)] public float knockbackForce;
    [Min(0f)] public float hitStunTime;
    public bool releaseBallOnHit;
    [Min(0f)] public float ballKnockbackForce;
    public string animationTrigger;
    [Min(0.01f)] public float animationSpeed;

    public CombatActionDefinition(CombatActionId id, float cooldown, float range, float radius, float knockbackForce, float hitStunTime, bool releaseBallOnHit, float ballKnockbackForce, string animationTrigger, float animationSpeed)
    {
        this.id = id;
        this.cooldown = cooldown;
        this.range = range;
        this.radius = radius;
        this.knockbackForce = knockbackForce;
        this.hitStunTime = hitStunTime;
        this.releaseBallOnHit = releaseBallOnHit;
        this.ballKnockbackForce = ballKnockbackForce;
        this.animationTrigger = animationTrigger;
        this.animationSpeed = animationSpeed;
    }
}
