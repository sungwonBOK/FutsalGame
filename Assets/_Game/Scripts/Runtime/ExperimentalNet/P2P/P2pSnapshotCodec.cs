using System;
using UnityEngine;

public readonly struct P2pPlayerSnapshot
{
    public ushort Sequence { get; }
    public Vector3 Position { get; }
    public float YawDegrees { get; }

    public P2pPlayerSnapshot(ushort sequence, Vector3 position, float yawDegrees)
    {
        Sequence = sequence;
        Position = position;
        YawDegrees = yawDegrees;
    }
}

public static class P2pSnapshotCodec
{
    public const int PacketSize = sizeof(ushort) + (sizeof(float) * 4);

    public static bool TryEncode(P2pPlayerSnapshot snapshot, out byte[] payload)
    {
        if (!IsFinite(snapshot.Position) || float.IsNaN(snapshot.YawDegrees) || float.IsInfinity(snapshot.YawDegrees))
        {
            payload = null;
            return false;
        }

        payload = new byte[PacketSize];
        WriteUInt16(payload, 0, snapshot.Sequence);
        WriteFloat(payload, 2, snapshot.Position.x);
        WriteFloat(payload, 6, snapshot.Position.y);
        WriteFloat(payload, 10, snapshot.Position.z);
        WriteFloat(payload, 14, snapshot.YawDegrees);
        return true;
    }

    public static bool TryDecode(byte[] payload, out P2pPlayerSnapshot snapshot)
    {
        snapshot = default;

        if (payload == null || payload.Length != PacketSize)
            return false;

        ushort sequence = BitConverter.ToUInt16(payload, 0);
        Vector3 position = new Vector3(
            BitConverter.ToSingle(payload, 2),
            BitConverter.ToSingle(payload, 6),
            BitConverter.ToSingle(payload, 10));
        float yawDegrees = BitConverter.ToSingle(payload, 14);

        if (!IsFinite(position) || float.IsNaN(yawDegrees) || float.IsInfinity(yawDegrees))
            return false;

        snapshot = new P2pPlayerSnapshot(sequence, position, yawDegrees);
        return true;
    }

    private static void WriteUInt16(byte[] payload, int offset, ushort value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, payload, offset, sizeof(ushort));
    }

    private static void WriteFloat(byte[] payload, int offset, float value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, payload, offset, sizeof(float));
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
