using UnityEngine;

public enum DefenseBlockDirection
{
    Left,
    Right,
    Back
}

public sealed class DefenseWindow
{
    private const float Duration = 1.5f;

    private float endsAt = float.NegativeInfinity;

    public bool IsActive(float now)
    {
        return now < endsAt;
    }

    public void Begin(float now)
    {
        endsAt = now + Duration;
    }

    public bool TryBlock(
        float now,
        Vector3 defenderPosition,
        Vector3 defenderForward,
        Vector3 attackerPosition,
        out DefenseBlockDirection direction)
    {
        direction = DefenseBlockDirection.Right;
        if (!IsActive(now))
            return false;

        endsAt = float.NegativeInfinity;
        direction = ResolveDirection(defenderPosition, defenderForward, attackerPosition);
        return true;
    }

    public static DefenseBlockDirection ResolveDirection(
        Vector3 defenderPosition,
        Vector3 defenderForward,
        Vector3 attackerPosition)
    {
        Vector3 forward = CharacterMovementUtility.FlattenOrFallback(defenderForward, Vector3.forward);
        Vector3 attackerDirection = CharacterMovementUtility.FlattenOrFallback(
            attackerPosition - defenderPosition,
            forward);
        float clockwiseAngle = Vector3.SignedAngle(forward, attackerDirection, Vector3.up);
        if (clockwiseAngle < 0f)
            clockwiseAngle += 360f;

        if (clockwiseAngle < 120f)
            return DefenseBlockDirection.Right;
        if (clockwiseAngle < 240f)
            return DefenseBlockDirection.Back;
        return DefenseBlockDirection.Left;
    }
}
