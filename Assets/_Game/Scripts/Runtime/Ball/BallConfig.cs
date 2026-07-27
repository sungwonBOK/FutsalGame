using System;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Futsal Brawl/Ball/Ball Config")]
public class BallConfig : ScriptableObject
{
    [Serializable]
    public struct PossessionSettings
    {
        [Min(0f)] public float acquireRange;
        [Min(0f)] public float ownerMaxDistance;
        [Min(0f)] public float reacquireDelay;
        [Min(0f)] public float ownershipTransferTime;

        public PossessionSettings(float acquireRange, float ownerMaxDistance, float reacquireDelay, float ownershipTransferTime)
        {
            this.acquireRange = acquireRange;
            this.ownerMaxDistance = ownerMaxDistance;
            this.reacquireDelay = reacquireDelay;
            this.ownershipTransferTime = ownershipTransferTime;
        }
    }

    [Serializable]
    public struct DribbleSettings
    {
        public Vector3 offset;
        [Min(0f)] public float followSharpness;
        [Min(0f)] public float maxFollowLag;
        [Min(0f)] public float detachImpulse;
        [Min(0.01f)] public float sprintTouchInterval;
        [Min(0f)] public float sprintTouchForce;
        [Min(1f)] public float possessionSprintTouchMultiplier;
        [Min(1f)] public float burstSprintTouchMultiplier;

        public DribbleSettings(
            Vector3 offset,
            float followSharpness,
            float detachImpulse,
            float sprintTouchInterval,
            float sprintTouchForce)
        {
            this.offset = offset;
            this.followSharpness = followSharpness;
            maxFollowLag = 0.45f;
            this.detachImpulse = detachImpulse;
            this.sprintTouchInterval = sprintTouchInterval;
            this.sprintTouchForce = sprintTouchForce;
            possessionSprintTouchMultiplier = 1f;
            burstSprintTouchMultiplier = 1.4f;
        }
    }

    [Serializable]
    public struct PassSettings
    {
        [FormerlySerializedAs("force")]
        [Min(0f)] public float minChargeForce;
        [Min(0f)] public float maxChargeForce;

        public PassSettings(float minChargeForce, float maxChargeForce)
        {
            this.minChargeForce = minChargeForce;
            this.maxChargeForce = maxChargeForce;
        }
    }

    [Serializable]
    public struct ShotSettings
    {
        [FormerlySerializedAs("passForce")]
        [Min(0f)] public float minChargeForce;
        [Min(0f)] public float shootForce;
        [Min(0f)] public float maxShootForce;
        [Min(0.01f)] public float maxChargeTime;
        [Min(0f)] public float cooldown;
        [Min(0f)] public float loftPerForce;
        [Range(0f, 1f)] public float momentumInherit;
        [Min(0f)] public float firstTouchWindow;
        [Min(1f)] public float firstTouchBonus;

        public ShotSettings(float minChargeForce, float shootForce, float maxShootForce, float maxChargeTime, float cooldown)
        {
            this.minChargeForce = minChargeForce;
            this.shootForce = shootForce;
            this.maxShootForce = maxShootForce;
            this.maxChargeTime = maxChargeTime;
            this.cooldown = cooldown;
            loftPerForce = 0.15f;
            momentumInherit = 0.5f;
            firstTouchWindow = 0.35f;
            firstTouchBonus = 1.3f;
        }
    }

    [Serializable]
    public struct PhysicsSettings
    {
        [Min(0f)] public float maxSpeed;
        [Min(0f)] public float releaseAngularVelocityLimit;

        public PhysicsSettings(float maxSpeed, float releaseAngularVelocityLimit)
        {
            this.maxSpeed = maxSpeed;
            this.releaseAngularVelocityLimit = releaseAngularVelocityLimit;
        }
    }

    [Header("Possession")]
    public PossessionSettings Possession = new PossessionSettings(1.2f, 2.2f, 0.4f, 0f);

    [Header("Dribble")]
    public DribbleSettings Dribble = new DribbleSettings(new Vector3(0f, -0.6f, 0.9f), 0f, 0f, 0.5f, 3.5f);

    [Header("Pass")]
    public PassSettings Pass = new PassSettings(3.5f, 7f);

    [Header("Shot")]
    public ShotSettings Shot = new ShotSettings(3.5f, 6f, 13f, 1f, 0.4f);

    [Header("Physics")]
    public PhysicsSettings Physics = new PhysicsSettings(0f, 0f);

    public float DribbleFollowSharpness => Dribble.followSharpness > 0f ? Dribble.followSharpness : 18f;
    public float DribbleMaxFollowLag => Dribble.maxFollowLag > 0f ? Dribble.maxFollowLag : 0.45f;
    public float ShotLoftPerForce => Shot.loftPerForce > 0f ? Shot.loftPerForce : 0.15f;
    public float ShotMomentumInherit => Shot.momentumInherit > 0f ? Shot.momentumInherit : 0.5f;
    public float FirstTouchWindow => Shot.firstTouchWindow > 0f ? Shot.firstTouchWindow : 0.35f;
    public float FirstTouchBonus => Shot.firstTouchBonus > 1f ? Shot.firstTouchBonus : 1.3f;
    public float PossessionSprintTouchMultiplier => Dribble.possessionSprintTouchMultiplier > 0f ? Dribble.possessionSprintTouchMultiplier : 1f;
    public float BurstSprintTouchMultiplier => Dribble.burstSprintTouchMultiplier > 0f ? Dribble.burstSprintTouchMultiplier : 1.4f;
}
