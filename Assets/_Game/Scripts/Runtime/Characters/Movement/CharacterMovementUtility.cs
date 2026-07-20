using UnityEngine;

public static class CharacterMovementUtility
{
    public static Vector2 ClampInput(Vector2 input)
    {
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    public static Vector3 BuildPlanarMoveDirection(Vector2 input)
    {
        Vector2 clamped = ClampInput(input);
        return new Vector3(clamped.x, 0f, clamped.y);
    }

    public static Vector3 BuildCameraRelativeMoveDirection(Vector2 input, Transform reference)
    {
        Vector2 clamped = ClampInput(input);
        if (clamped.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        if (reference == null)
            return BuildPlanarMoveDirection(clamped);

        Vector3 forward = reference.forward;
        Vector3 right = reference.right;
        forward.y = 0f;
        right.y = 0f;

        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

        Vector3 direction = right * clamped.x + forward * clamped.y;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    public static Vector3 NormalizePlanar(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    public static Vector3 ClampPlanar(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    public static Vector3 ResolveActionDirection(bool hasMoveInput, Vector3 moveDirection, Vector3 characterForward)
    {
        if (hasMoveInput)
            return NormalizePlanar(moveDirection);

        return FlattenOrFallback(characterForward, Vector3.forward);
    }

    public static Vector3 FlattenOrFallback(Vector3 direction, Vector3 fallback)
    {
        Vector3 flattened = NormalizePlanar(direction);
        if (flattened.sqrMagnitude > 0.0001f)
            return flattened;

        flattened = NormalizePlanar(fallback);
        return flattened.sqrMagnitude > 0.0001f ? flattened : Vector3.forward;
    }

    public static CharacterMovementProfile SanitizeProfile(
        CharacterMovementProfile profile,
        float fallbackSpeed,
        float fallbackRotationSpeed)
    {
        if (profile.speed <= 0f)
            profile.speed = Mathf.Max(0f, fallbackSpeed);
        if (profile.acceleration <= 0f)
            profile.acceleration = Mathf.Max(1f, profile.speed * 8f);
        if (profile.deceleration <= 0f)
            profile.deceleration = Mathf.Max(1f, profile.acceleration);
        if (profile.rotationSpeed <= 0f)
            profile.rotationSpeed = Mathf.Max(1f, fallbackRotationSpeed);
        return profile;
    }
}
