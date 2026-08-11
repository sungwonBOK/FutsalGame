using System;
using UnityEngine;

public enum P2pCombatMessageKind : byte
{
    ActionStart = 1,
    InteractionRequest = 2,
    InteractionResult = 3,
    GrabStarted = 4,
    GrabReleased = 5,
    ActionCancel = 6
}

public enum P2pCombatActionKind : byte
{
    Punch = 1,
    SlideTackle = 2,
    CrossPunch = 3,
    Grab = 4,
    PowerStun = 5
}

public enum P2pCombatResolution : byte
{
    Hit = 1,
    Block = 2,
    Evade = 3
}

public readonly struct P2pCombatMessage
{
    public P2pCombatMessageKind Kind { get; }
    public uint ActionId { get; }
    public ushort Sequence { get; }
    public P2pCombatActionKind ActionKind { get; }
    public P2pCombatResolution Resolution { get; }
    public Vector3 Origin { get; }
    public Vector3 Direction { get; }

    public P2pCombatMessage(
        P2pCombatMessageKind kind,
        uint actionId,
        ushort sequence,
        P2pCombatActionKind actionKind,
        P2pCombatResolution resolution,
        Vector3 origin,
        Vector3 direction)
    {
        Kind = kind;
        ActionId = actionId;
        Sequence = sequence;
        ActionKind = actionKind;
        Resolution = resolution;
        Origin = origin;
        Direction = direction;
    }
}

/// <summary>Fixed-size, versioned payloads for the reliable direct-P2P combat channel.</summary>
public static class P2pCombatCodec
{
    private const byte Version = 1;
    public const int PacketSize = 34;

    public static bool TryEncode(P2pCombatMessage message, out byte[] payload)
    {
        payload = null;
        if (!IsKnown(message.Kind) || !IsKnown(message.ActionKind) || !IsKnown(message.Resolution))
            return false;

        payload = new byte[PacketSize];
        payload[0] = Version;
        payload[1] = (byte)message.Kind;
        WriteUInt32(payload, 2, message.ActionId);
        WriteUInt16(payload, 6, message.Sequence);
        payload[8] = (byte)message.ActionKind;
        payload[9] = (byte)message.Resolution;
        WriteVector3(payload, 10, message.Origin);
        WriteVector3(payload, 22, message.Direction);
        return true;
    }

    public static bool TryDecode(byte[] payload, out P2pCombatMessage message)
    {
        message = default;
        if (payload == null || payload.Length != PacketSize)
            return false;

        if (payload[0] != Version)
            return false;

        P2pCombatMessageKind kind = (P2pCombatMessageKind)payload[1];
        uint actionId = BitConverter.ToUInt32(payload, 2);
        ushort sequence = BitConverter.ToUInt16(payload, 6);
        P2pCombatActionKind actionKind = (P2pCombatActionKind)payload[8];
        P2pCombatResolution resolution = (P2pCombatResolution)payload[9];
        Vector3 origin = ReadVector3(payload, 10);
        Vector3 direction = ReadVector3(payload, 22);
        if (!IsKnown(kind) || !IsKnown(actionKind) || !IsKnown(resolution) || actionId == 0 || !IsFinite(origin) || !IsFinite(direction))
            return false;

        message = new P2pCombatMessage(kind, actionId, sequence, actionKind, resolution, origin, direction);
        return true;
    }

    private static void WriteUInt16(byte[] payload, int offset, ushort value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, payload, offset, sizeof(ushort));
    }

    private static void WriteUInt32(byte[] payload, int offset, uint value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, payload, offset, sizeof(uint));
    }

    private static void WriteVector3(byte[] payload, int offset, Vector3 value)
    {
        WriteFloat(payload, offset, value.x);
        WriteFloat(payload, offset + sizeof(float), value.y);
        WriteFloat(payload, offset + (sizeof(float) * 2), value.z);
    }

    private static Vector3 ReadVector3(byte[] payload, int offset)
    {
        return new Vector3(
            BitConverter.ToSingle(payload, offset),
            BitConverter.ToSingle(payload, offset + sizeof(float)),
            BitConverter.ToSingle(payload, offset + (sizeof(float) * 2)));
    }

    private static void WriteFloat(byte[] payload, int offset, float value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, payload, offset, sizeof(float));
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
    }

    private static bool IsKnown(P2pCombatMessageKind value) => value >= P2pCombatMessageKind.ActionStart && value <= P2pCombatMessageKind.ActionCancel;
    private static bool IsKnown(P2pCombatActionKind value) => value >= P2pCombatActionKind.Punch && value <= P2pCombatActionKind.PowerStun;
    private static bool IsKnown(P2pCombatResolution value) => value >= P2pCombatResolution.Hit && value <= P2pCombatResolution.Evade;
}

/// <summary>Presentation rules for interaction results received by the attacking peer.</summary>
public static class P2pCombatPresentationPolicy
{
    public static bool ShouldReplayRemoteBlock(P2pCombatResolution resolution)
    {
        return resolution == P2pCombatResolution.Block;
    }
}
