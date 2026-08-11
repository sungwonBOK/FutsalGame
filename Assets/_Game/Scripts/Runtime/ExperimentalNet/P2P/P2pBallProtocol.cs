using System;
using UnityEngine;

public enum P2pBallEventKind : byte
{
    AuthorityChanged = 1,
    Action = 2,
    AcquireRequest = 3
}

public enum P2pBallActionKind : byte
{
    None = 0,
    Pass = 1,
    Shot = 2,
    LobPass = 3
}

/// <summary>Latest-only state published by the single current ball authority.</summary>
public struct P2pBallState
{
    private readonly ulong authorityId;
    private readonly ulong ownerId;
    private readonly uint epoch;
    private readonly ushort sequence;
    private readonly Vector3 position;
    private readonly Quaternion rotation;
    private readonly Vector3 velocity;
    private readonly Vector3 angularVelocity;

    public ulong AuthorityId { get { return authorityId; } }
    public ulong OwnerId { get { return ownerId; } }
    public uint Epoch { get { return epoch; } }
    public ushort Sequence { get { return sequence; } }
    public Vector3 Position { get { return position; } }
    public Quaternion Rotation { get { return rotation; } }
    public Vector3 Velocity { get { return velocity; } }
    public Vector3 AngularVelocity { get { return angularVelocity; } }

    public P2pBallState(
        ulong authorityId,
        ulong ownerId,
        uint epoch,
        ushort sequence,
        Vector3 position,
        Quaternion rotation,
        Vector3 velocity,
        Vector3 angularVelocity)
    {
        this.authorityId = authorityId;
        this.ownerId = ownerId;
        this.epoch = epoch;
        this.sequence = sequence;
        this.position = position;
        this.rotation = rotation;
        this.velocity = velocity;
        this.angularVelocity = angularVelocity;
    }
}

/// <summary>Reliable event carrying a pass/shot start or an atomic authority-transfer anchor.</summary>
public struct P2pBallEvent
{
    private readonly P2pBallEventKind kind;
    private readonly P2pBallActionKind actionKind;
    private readonly uint actionId;
    private readonly ulong sourceAuthorityId;
    private readonly P2pBallState anchorState;

    public P2pBallEventKind Kind { get { return kind; } }
    public P2pBallActionKind ActionKind { get { return actionKind; } }
    public uint ActionId { get { return actionId; } }
    public ulong SourceAuthorityId { get { return sourceAuthorityId; } }
    public P2pBallState AnchorState { get { return anchorState; } }

    public P2pBallEvent(
        P2pBallEventKind kind,
        P2pBallActionKind actionKind,
        uint actionId,
        ulong sourceAuthorityId,
        P2pBallState anchorState)
    {
        this.kind = kind;
        this.actionKind = actionKind;
        this.actionId = actionId;
        this.sourceAuthorityId = sourceAuthorityId;
        this.anchorState = anchorState;
    }
}

/// <summary>Reliable request from a non-authority player attempting to acquire a free ball.</summary>
public struct P2pBallAcquireRequest
{
    private readonly uint actionId;
    private readonly ulong claimantId;
    private readonly uint observedEpoch;

    public uint ActionId { get { return actionId; } }
    public ulong ClaimantId { get { return claimantId; } }
    public uint ObservedEpoch { get { return observedEpoch; } }

    public P2pBallAcquireRequest(uint actionId, ulong claimantId, uint observedEpoch)
    {
        this.actionId = actionId;
        this.claimantId = claimantId;
        this.observedEpoch = observedEpoch;
    }
}

public static class P2pBallStateCodec
{
    private const byte Version = 1;
    private const int StateDataSize = 74;
    public const int PacketSize = 1 + StateDataSize;

    public static bool TryEncode(P2pBallState state, out byte[] payload)
    {
        payload = null;
        if (!IsValidState(state))
            return false;

        payload = new byte[PacketSize];
        payload[0] = Version;
        WriteState(payload, 1, state);
        return true;
    }

    public static bool TryDecode(byte[] payload, out P2pBallState state)
    {
        state = default(P2pBallState);
        if (payload == null || payload.Length != PacketSize || payload[0] != Version)
            return false;

        return TryReadState(payload, 1, out state) && IsValidState(state);
    }

    internal static bool IsValidState(P2pBallState state)
    {
        return state.AuthorityId != 0
            && IsFinite(state.Position)
            && IsFinite(state.Rotation)
            && IsFinite(state.Velocity)
            && IsFinite(state.AngularVelocity);
    }

    internal static void WriteState(byte[] payload, int offset, P2pBallState state)
    {
        WriteUInt64(payload, offset, state.AuthorityId);
        WriteUInt64(payload, offset + 8, state.OwnerId);
        WriteUInt32(payload, offset + 16, state.Epoch);
        WriteUInt16(payload, offset + 20, state.Sequence);
        WriteVector3(payload, offset + 22, state.Position);
        WriteQuaternion(payload, offset + 34, state.Rotation);
        WriteVector3(payload, offset + 50, state.Velocity);
        WriteVector3(payload, offset + 62, state.AngularVelocity);
    }

    internal static bool TryReadState(byte[] payload, int offset, out P2pBallState state)
    {
        state = new P2pBallState(
            ReadUInt64(payload, offset),
            ReadUInt64(payload, offset + 8),
            ReadUInt32(payload, offset + 16),
            ReadUInt16(payload, offset + 20),
            ReadVector3(payload, offset + 22),
            ReadQuaternion(payload, offset + 34),
            ReadVector3(payload, offset + 50),
            ReadVector3(payload, offset + 62));
        return true;
    }

    internal static void WriteUInt16(byte[] payload, int offset, ushort value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, payload, offset, sizeof(ushort));
    }

    internal static void WriteUInt32(byte[] payload, int offset, uint value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, payload, offset, sizeof(uint));
    }

    internal static void WriteUInt64(byte[] payload, int offset, ulong value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, payload, offset, sizeof(ulong));
    }

    internal static void WriteVector3(byte[] payload, int offset, Vector3 value)
    {
        WriteFloat(payload, offset, value.x);
        WriteFloat(payload, offset + 4, value.y);
        WriteFloat(payload, offset + 8, value.z);
    }

    internal static void WriteQuaternion(byte[] payload, int offset, Quaternion value)
    {
        WriteFloat(payload, offset, value.x);
        WriteFloat(payload, offset + 4, value.y);
        WriteFloat(payload, offset + 8, value.z);
        WriteFloat(payload, offset + 12, value.w);
    }

    internal static ushort ReadUInt16(byte[] payload, int offset)
    {
        return BitConverter.ToUInt16(payload, offset);
    }

    internal static uint ReadUInt32(byte[] payload, int offset)
    {
        return BitConverter.ToUInt32(payload, offset);
    }

    internal static ulong ReadUInt64(byte[] payload, int offset)
    {
        return BitConverter.ToUInt64(payload, offset);
    }

    internal static Vector3 ReadVector3(byte[] payload, int offset)
    {
        return new Vector3(
            BitConverter.ToSingle(payload, offset),
            BitConverter.ToSingle(payload, offset + 4),
            BitConverter.ToSingle(payload, offset + 8));
    }

    internal static Quaternion ReadQuaternion(byte[] payload, int offset)
    {
        return new Quaternion(
            BitConverter.ToSingle(payload, offset),
            BitConverter.ToSingle(payload, offset + 4),
            BitConverter.ToSingle(payload, offset + 8),
            BitConverter.ToSingle(payload, offset + 12));
    }

    private static void WriteFloat(byte[] payload, int offset, float value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, payload, offset, sizeof(float));
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public static class P2pBallEventCodec
{
    private const byte Version = 1;
    public const int PacketSize = 15 + 74;

    public static bool TryEncode(P2pBallEvent message, out byte[] payload)
    {
        payload = null;
        if (!IsValid(message))
            return false;

        payload = new byte[PacketSize];
        payload[0] = Version;
        payload[1] = (byte)message.Kind;
        payload[2] = (byte)message.ActionKind;
        P2pBallStateCodec.WriteUInt32(payload, 3, message.ActionId);
        P2pBallStateCodec.WriteUInt64(payload, 7, message.SourceAuthorityId);
        P2pBallStateCodec.WriteState(payload, 15, message.AnchorState);
        return true;
    }

    public static bool TryDecode(byte[] payload, out P2pBallEvent message)
    {
        message = default(P2pBallEvent);
        if (payload == null || payload.Length != PacketSize || payload[0] != Version)
            return false;

        P2pBallEventKind kind = (P2pBallEventKind)payload[1];
        P2pBallActionKind actionKind = (P2pBallActionKind)payload[2];
        P2pBallState state;
        P2pBallStateCodec.TryReadState(payload, 15, out state);
        message = new P2pBallEvent(
            kind,
            actionKind,
            P2pBallStateCodec.ReadUInt32(payload, 3),
            P2pBallStateCodec.ReadUInt64(payload, 7),
            state);
        return IsValid(message);
    }

    private static bool IsValid(P2pBallEvent message)
    {
        if (message.ActionId == 0 || message.SourceAuthorityId == 0 || !P2pBallStateCodec.IsValidState(message.AnchorState))
            return false;

        if (message.Kind == P2pBallEventKind.AuthorityChanged)
            return message.ActionKind == P2pBallActionKind.None;

        return message.Kind == P2pBallEventKind.Action
            && (message.ActionKind == P2pBallActionKind.Pass
                || message.ActionKind == P2pBallActionKind.Shot
                || message.ActionKind == P2pBallActionKind.LobPass);
    }
}

public static class P2pBallAcquireRequestCodec
{
    private const byte Version = 1;
    private const byte PacketKind = 3;
    public const int PacketSize = 18;

    public static bool TryEncode(P2pBallAcquireRequest request, out byte[] payload)
    {
        payload = null;
        if (request.ActionId == 0 || request.ClaimantId == 0)
            return false;

        payload = new byte[PacketSize];
        payload[0] = Version;
        payload[1] = PacketKind;
        P2pBallStateCodec.WriteUInt32(payload, 2, request.ActionId);
        P2pBallStateCodec.WriteUInt64(payload, 6, request.ClaimantId);
        P2pBallStateCodec.WriteUInt32(payload, 14, request.ObservedEpoch);
        return true;
    }

    public static bool TryDecode(byte[] payload, out P2pBallAcquireRequest request)
    {
        request = default(P2pBallAcquireRequest);
        if (payload == null || payload.Length != PacketSize || payload[0] != Version || payload[1] != PacketKind)
            return false;

        request = new P2pBallAcquireRequest(
            P2pBallStateCodec.ReadUInt32(payload, 2),
            P2pBallStateCodec.ReadUInt64(payload, 6),
            P2pBallStateCodec.ReadUInt32(payload, 14));
        return request.ActionId != 0 && request.ClaimantId != 0;
    }
}
