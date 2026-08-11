using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// Uses the NGO/MPS control plane only to route addressed WebRTC setup
/// fragments. The Host forwards client-to-client setup packets unchanged; it
/// never receives or forwards gameplay packets.
/// </summary>
public sealed class P2pLobbySignalRelay : IPeerSignalingTransport
{
    private const string ClientToHostMessageName = "Futsal.P2P.ClientToHost";
    private const string HostToClientMessageName = "Futsal.P2P.HostToClient";
    private const int FragmentHeaderBytes =
        sizeof(byte) + sizeof(ushort) + sizeof(byte) + sizeof(byte) + sizeof(ushort) + sizeof(ulong) + sizeof(ulong);

    private readonly NetworkManager networkManager;
    private readonly Dictionary<ulong, P2pSignalReassembler> reassemblersBySender =
        new Dictionary<ulong, P2pSignalReassembler>();
    private readonly string incomingMessageName;
    private readonly string outgoingMessageName;
    private ushort nextMessageId;
    private bool isStarted;

    public event Action<P2pPeerSignal> SignalReceived;

    public P2pLobbySignalRelay(NetworkManager networkManager)
    {
        this.networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
        incomingMessageName = networkManager.IsServer ? ClientToHostMessageName : HostToClientMessageName;
        outgoingMessageName = networkManager.IsServer ? HostToClientMessageName : ClientToHostMessageName;
    }

    public void Start()
    {
        if (isStarted)
            return;

        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(incomingMessageName, ReceiveFragment);
        isStarted = true;
    }

    public void Stop()
    {
        if (!isStarted)
            return;

        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(incomingMessageName);
        reassemblersBySender.Clear();
        isStarted = false;
    }

    public bool TrySend(P2pPeerSignal peerSignal, out string error)
    {
        error = null;
        if (!isStarted || !networkManager.IsListening)
        {
            error = "P2P signaling is not ready.";
            return false;
        }

        if (peerSignal.SenderClientId != networkManager.LocalClientId
            || peerSignal.RecipientClientId == networkManager.LocalClientId
            || !IsConnected(peerSignal.RecipientClientId))
        {
            error = "The P2P signal must target one connected remote peer.";
            return false;
        }

        try
        {
            ulong transportReceiver = networkManager.IsServer
                ? peerSignal.RecipientClientId
                : NetworkManager.ServerClientId;
            foreach (P2pSignalFragment fragment in P2pSignalFragmenter.Split(peerSignal.Signal, nextMessageId++))
                SendFragment(transportReceiver, peerSignal.SenderClientId, peerSignal.RecipientClientId, fragment);

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private void ReceiveFragment(ulong transportSenderClientId, FastBufferReader reader)
    {
        if (!TryReadFragment(reader, out P2pSignalEnvelopeFragment fragment))
            return;

        if (networkManager.IsServer)
        {
            ReceiveAtHost(transportSenderClientId, fragment);
            return;
        }

        if (transportSenderClientId != NetworkManager.ServerClientId
            || fragment.RecipientClientId != networkManager.LocalClientId)
        {
            return;
        }

        TryDeliver(fragment);
    }

    private void ReceiveAtHost(ulong transportSenderClientId, P2pSignalEnvelopeFragment fragment)
    {
        if (transportSenderClientId != fragment.SenderClientId || !IsConnected(fragment.RecipientClientId))
            return;

        if (fragment.RecipientClientId == networkManager.LocalClientId)
        {
            TryDeliver(fragment);
            return;
        }

        SendFragment(
            fragment.RecipientClientId,
            fragment.SenderClientId,
            fragment.RecipientClientId,
            fragment.Fragment);
    }

    private void TryDeliver(P2pSignalEnvelopeFragment envelope)
    {
        if (!reassemblersBySender.TryGetValue(envelope.SenderClientId, out P2pSignalReassembler reassembler))
        {
            reassembler = new P2pSignalReassembler();
            reassemblersBySender.Add(envelope.SenderClientId, reassembler);
        }

        if (!reassembler.TryAdd(envelope.Fragment, out P2pSignalMessage message))
            return;

        if (P2pPeerSignal.TryCreate(envelope.SenderClientId, envelope.RecipientClientId, message, out P2pPeerSignal signal))
            SignalReceived?.Invoke(signal);
    }

    private bool IsConnected(ulong clientId)
    {
        if (!networkManager.IsListening)
            return false;

        if (networkManager.IsServer)
        {
            foreach (ulong connectedClientId in networkManager.ConnectedClientsIds)
            {
                if (connectedClientId == clientId)
                    return true;
            }

            return false;
        }

        return clientId == NetworkManager.ServerClientId;
    }

    private void SendFragment(
        ulong transportReceiverClientId,
        ulong senderClientId,
        ulong recipientClientId,
        P2pSignalFragment fragment)
    {
        int writerSize = FragmentHeaderBytes + fragment.Payload.Length;
        using FastBufferWriter writer = new FastBufferWriter(writerSize, Allocator.Temp);
        writer.WriteValueSafe((byte)fragment.Kind);
        writer.WriteValueSafe(fragment.MessageId);
        writer.WriteValueSafe(fragment.Index);
        writer.WriteValueSafe(fragment.Count);
        writer.WriteValueSafe((ushort)fragment.Payload.Length);
        writer.WriteValueSafe(senderClientId);
        writer.WriteValueSafe(recipientClientId);
        writer.WriteBytesSafe(fragment.Payload);
        networkManager.CustomMessagingManager.SendNamedMessage(outgoingMessageName, transportReceiverClientId, writer);
    }

    private static bool TryReadFragment(FastBufferReader reader, out P2pSignalEnvelopeFragment envelope)
    {
        envelope = default;
        try
        {
            reader.ReadValueSafe(out byte kindValue);
            reader.ReadValueSafe(out ushort messageId);
            reader.ReadValueSafe(out byte index);
            reader.ReadValueSafe(out byte count);
            reader.ReadValueSafe(out ushort payloadLength);
            reader.ReadValueSafe(out ulong senderClientId);
            reader.ReadValueSafe(out ulong recipientClientId);

            if (payloadLength == 0 || payloadLength > P2pSignalFragmenter.MaxFragmentPayloadBytes)
                return false;

            byte[] payload = new byte[payloadLength];
            reader.ReadBytesSafe(ref payload, payloadLength);
            envelope = new P2pSignalEnvelopeFragment(
                senderClientId,
                recipientClientId,
                new P2pSignalFragment((P2pSignalKind)kindValue, messageId, index, count, payload));
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private readonly struct P2pSignalEnvelopeFragment
    {
        public ulong SenderClientId { get; }
        public ulong RecipientClientId { get; }
        public P2pSignalFragment Fragment { get; }

        public P2pSignalEnvelopeFragment(ulong senderClientId, ulong recipientClientId, P2pSignalFragment fragment)
        {
            SenderClientId = senderClientId;
            RecipientClientId = recipientClientId;
            Fragment = fragment;
        }
    }
}
