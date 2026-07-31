using System;
using System.Collections.Generic;
using System.Text;

public readonly struct P2pSignalFragment
{
    public P2pSignalKind Kind { get; }
    public ushort MessageId { get; }
    public byte Index { get; }
    public byte Count { get; }
    public byte[] Payload { get; }

    public P2pSignalFragment(P2pSignalKind kind, ushort messageId, byte index, byte count, byte[] payload)
    {
        Kind = kind;
        MessageId = messageId;
        Index = index;
        Count = count;
        Payload = payload;
    }
}

public static class P2pSignalFragmenter
{
    // NGO named messages are intentionally kept well below their non-fragmented size limit.
    public const int MaxFragmentPayloadBytes = 900;
    public const int MaxFragmentsPerMessage = 64;

    public static IReadOnlyList<P2pSignalFragment> Split(P2pSignalMessage message, ushort messageId)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message.Payload);
        int fragmentCount = (bytes.Length + MaxFragmentPayloadBytes - 1) / MaxFragmentPayloadBytes;

        if (fragmentCount == 0 || fragmentCount > MaxFragmentsPerMessage)
            throw new ArgumentOutOfRangeException(nameof(message), "The P2P setup message is too large to relay.");

        P2pSignalFragment[] fragments = new P2pSignalFragment[fragmentCount];
        for (int index = 0; index < fragmentCount; index++)
        {
            int offset = index * MaxFragmentPayloadBytes;
            int length = Math.Min(MaxFragmentPayloadBytes, bytes.Length - offset);
            byte[] payload = new byte[length];
            Buffer.BlockCopy(bytes, offset, payload, 0, length);
            fragments[index] = new P2pSignalFragment(message.Kind, messageId, (byte)index, (byte)fragmentCount, payload);
        }

        return fragments;
    }
}

public sealed class P2pSignalReassembler
{
    private readonly Dictionary<ushort, PendingMessage> pendingMessages = new Dictionary<ushort, PendingMessage>();

    public bool TryAdd(P2pSignalFragment fragment, out P2pSignalMessage message)
    {
        message = default;

        if (!IsValid(fragment))
            return false;

        if (!pendingMessages.TryGetValue(fragment.MessageId, out PendingMessage pending))
        {
            pending = new PendingMessage(fragment.Kind, fragment.Count);
            pendingMessages.Add(fragment.MessageId, pending);
        }

        if (!pending.Accepts(fragment))
        {
            pendingMessages.Remove(fragment.MessageId);
            return false;
        }

        pending.Add(fragment);
        if (!pending.IsComplete)
            return false;

        pendingMessages.Remove(fragment.MessageId);
        return P2pSignalMessage.TryCreate(fragment.Kind, pending.ToPayload(), out message);
    }

    public void Clear() => pendingMessages.Clear();

    private static bool IsValid(P2pSignalFragment fragment)
    {
        return Enum.IsDefined(typeof(P2pSignalKind), fragment.Kind)
            && fragment.Count > 0
            && fragment.Count <= P2pSignalFragmenter.MaxFragmentsPerMessage
            && fragment.Index < fragment.Count
            && fragment.Payload != null
            && fragment.Payload.Length > 0
            && fragment.Payload.Length <= P2pSignalFragmenter.MaxFragmentPayloadBytes;
    }

    private sealed class PendingMessage
    {
        private readonly P2pSignalKind kind;
        private readonly byte[][] fragments;
        private int receivedCount;

        public bool IsComplete => receivedCount == fragments.Length;

        public PendingMessage(P2pSignalKind kind, byte fragmentCount)
        {
            this.kind = kind;
            fragments = new byte[fragmentCount][];
        }

        public bool Accepts(P2pSignalFragment fragment)
        {
            return fragment.Kind == kind && fragment.Count == fragments.Length;
        }

        public void Add(P2pSignalFragment fragment)
        {
            if (fragments[fragment.Index] != null)
                return;

            fragments[fragment.Index] = fragment.Payload;
            receivedCount++;
        }

        public string ToPayload()
        {
            int totalLength = 0;
            foreach (byte[] fragment in fragments)
                totalLength += fragment.Length;

            byte[] payload = new byte[totalLength];
            int offset = 0;
            foreach (byte[] fragment in fragments)
            {
                Buffer.BlockCopy(fragment, 0, payload, offset, fragment.Length);
                offset += fragment.Length;
            }

            return Encoding.UTF8.GetString(payload);
        }
    }
}
