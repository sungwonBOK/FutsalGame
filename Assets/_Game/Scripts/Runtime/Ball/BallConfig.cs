using System;
using UnityEngine;

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
        [Min(0f)] public float detachImpulse;

        public DribbleSettings(Vector3 offset, float followSharpness, float detachImpulse)
        {
            this.offset = offset;
            this.followSharpness = followSharpness;
            this.detachImpulse = detachImpulse;
        }
    }

    [Serializable]
    public struct ShotSettings
    {
        [Min(0f)] public float passForce;
        [Min(0f)] public float shootForce;
        [Min(0f)] public float maxShootForce;
        [Min(0.01f)] public float maxChargeTime;
        [Min(0f)] public float cooldown;

        public ShotSettings(float passForce, float shootForce, float maxShootForce, float maxChargeTime, float cooldown)
        {
            this.passForce = passForce;
            this.shootForce = shootForce;
            this.maxShootForce = maxShootForce;
            this.maxChargeTime = maxChargeTime;
            this.cooldown = cooldown;
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
    public DribbleSettings Dribble = new DribbleSettings(new Vector3(0f, -0.6f, 0.9f), 0f, 0f);

    [Header("Shot")]
    public ShotSettings Shot = new ShotSettings(3.5f, 6f, 13f, 1f, 0.4f);

    [Header("Physics")]
    public PhysicsSettings Physics = new PhysicsSettings(0f, 0f);
}
