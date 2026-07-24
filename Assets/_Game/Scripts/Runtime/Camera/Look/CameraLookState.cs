public readonly struct CameraLookState
{
    public CameraLookState(float yaw, float pitch)
    {
        Yaw = yaw;
        Pitch = pitch;
    }

    public float Yaw { get; }
    public float Pitch { get; }
}
