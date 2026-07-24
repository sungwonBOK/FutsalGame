using UnityEngine;

public enum CameraBaseMode
{
    ThirdPerson,
    Possession
}

public readonly struct CameraFramingProfile
{
    public CameraFramingProfile(float lookAtHeight, float lookForwardOffset, float distance, float height, float fovBias)
    {
        LookAtHeight = lookAtHeight;
        LookForwardOffset = lookForwardOffset;
        Distance = distance;
        Height = height;
        FovBias = fovBias;
    }

    public float LookAtHeight { get; }
    public float LookForwardOffset { get; }
    public float Distance { get; }
    public float Height { get; }
    public float FovBias { get; }

    public static CameraFramingProfile FromThirdPerson(ThirdPersonActionCameraSettings settings)
    {
        return new CameraFramingProfile(settings.lookAtHeight, 0f, settings.distance, settings.height, 0f);
    }

    public static CameraFramingProfile FromPossession(ThirdPersonActionCameraSettings settings)
    {
        return new CameraFramingProfile(
            settings.lookAtHeight,
            settings.possessionLookForwardOffset,
            settings.distance + settings.possessionDistanceOffset,
            settings.height + settings.possessionHeightOffset,
            settings.possessionFovBias);
    }
}

public readonly struct CameraModeResult
{
    public CameraModeResult(CameraBaseMode baseMode, Vector3 lookPoint, CameraFramingProfile framing)
    {
        BaseMode = baseMode;
        LookPoint = lookPoint;
        Framing = framing;
    }

    public CameraBaseMode BaseMode { get; }
    public Vector3 LookPoint { get; }
    public CameraFramingProfile Framing { get; }
}
