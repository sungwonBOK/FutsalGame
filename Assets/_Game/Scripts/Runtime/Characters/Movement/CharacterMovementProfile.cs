using System;
using UnityEngine;

[Serializable]
public struct CharacterMovementProfile
{
    [Min(0f)] public float speed;
    [Min(0f)] public float acceleration;
    [Min(0f)] public float deceleration;
    [Min(0f)] public float rotationSpeed;

    public CharacterMovementProfile(float speed, float acceleration, float deceleration, float rotationSpeed)
    {
        this.speed = speed;
        this.acceleration = acceleration;
        this.deceleration = deceleration;
        this.rotationSpeed = rotationSpeed;
    }
}
