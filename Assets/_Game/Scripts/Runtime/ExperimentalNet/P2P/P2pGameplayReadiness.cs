using System;

[Flags]
public enum P2pGameplayChannel : byte
{
    None = 0,
    Snapshot = 1 << 0,
    Combat = 1 << 1,
    Ball = 1 << 2
}

/// <summary>
/// Describes the direct gameplay channels required for a match phase without depending on WebRTC.
/// Each future gameplay subsystem can add its channel to the required set when it has a P2P path.
/// </summary>
public struct P2pGameplayReadiness
{
    private readonly P2pGameplayChannel requiredChannels;

    public P2pGameplayChannel RequiredChannels
    {
        get { return requiredChannels; }
    }

    public P2pGameplayReadiness(P2pGameplayChannel requiredChannels)
    {
        this.requiredChannels = requiredChannels;
    }

    public bool IsReady(P2pGameplayChannel openChannels)
    {
        return (openChannels & requiredChannels) == requiredChannels;
    }
}
