using System;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// Uses the existing NGO connection only to exchange WebRTC setup messages.
/// It never carries gameplay data.
/// </summary>
public sealed class P2pLobbySignalRelay
{
    private const string ClientToHostMessageName = "Futsal.P2P.ClientToHost";
    private const string HostToClientMessageName = "Futsal.P2P.HostToClient";
    private const int FragmentHeaderBytes = sizeof(byte) + sizeof(ushort) + sizeof(byte) + sizeof(byte) + sizeof(ushort);

    private readonly NetworkManager networkManager;
    private readonly P2pSignalReassembler reassembler = new P2pSignalReassembler();
    private readonly string incomingMessageName;
    private readonly string outgoingMessageName;
    private ushort nextMessageId;
    private bool isStarted;

    public event Action<P2pSignalMessage> SignalReceived;

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
        reassembler.Clear();
        isStarted = false;
    }

    public bool TrySend(P2pSignalMessage message, out string error)
    {
        error = null;

        if (!isStarted)
        {
            error = "P2P signaling is not ready.";
            return false;
        }

        if (!HasExactlyOneRemotePeer())
        {
            error = "A direct P2P game requires exactly two connected players.";
            return false;
        }

        try
        {
            foreach (P2pSignalFragment fragment in P2pSignalFragmenter.Split(message, nextMessageId++))
                SendFragment(fragment);

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private void ReceiveFragment(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsExpectedSender(senderClientId) || !TryReadFragment(reader, out P2pSignalFragment fragment))
            return;

        if (reassembler.TryAdd(fragment, out P2pSignalMessage message))
            SignalReceived?.Invoke(message);
    }

    private bool IsExpectedSender(ulong senderClientId)
    {
        if (networkManager.IsServer)
            return senderClientId != networkManager.LocalClientId && HasExactlyOneRemotePeer();

        return senderClientId == NetworkManager.ServerClientId;
    }

    private bool HasExactlyOneRemotePeer()
    {
        if (!networkManager.IsListening)
            return false;

        return networkManager.IsServer
            ? networkManager.ConnectedClientsIds.Count == 2
            : networkManager.LocalClientId != NetworkManager.ServerClientId;
    }

    private void SendFragment(P2pSignalFragment fragment)
    {
        int writerSize = FragmentHeaderBytes + fragment.Payload.Length;
        using FastBufferWriter writer = new FastBufferWriter(writerSize, Allocator.Temp);
        writer.WriteValueSafe((byte)fragment.Kind);
        writer.WriteValueSafe(fragment.MessageId);
        writer.WriteValueSafe(fragment.Index);
        writer.WriteValueSafe(fragment.Count);
        writer.WriteValueSafe((ushort)fragment.Payload.Length);
        writer.WriteBytesSafe(fragment.Payload);

        ulong receiverClientId = networkManager.IsServer
            ? FindRemoteClientId()
            : NetworkManager.ServerClientId;

        networkManager.CustomMessagingManager.SendNamedMessage(outgoingMessageName, receiverClientId, writer);
    }

    private ulong FindRemoteClientId()
    {
        foreach (ulong clientId in networkManager.ConnectedClientsIds)
        {
            if (clientId != networkManager.LocalClientId)
                return clientId;
        }

        throw new InvalidOperationException("No remote P2P peer is connected.");
    }

    private static bool TryReadFragment(FastBufferReader reader, out P2pSignalFragment fragment)
    {
        fragment = default;

        try
        {
            reader.ReadValueSafe(out byte kindValue);
            reader.ReadValueSafe(out ushort messageId);
            reader.ReadValueSafe(out byte index);
            reader.ReadValueSafe(out byte count);
            reader.ReadValueSafe(out ushort payloadLength);

            if (payloadLength == 0 || payloadLength > P2pSignalFragmenter.MaxFragmentPayloadBytes)
                return false;

            byte[] payload = new byte[payloadLength];
            reader.ReadBytesSafe(ref payload, payloadLength);
            fragment = new P2pSignalFragment((P2pSignalKind)kindValue, messageId, index, count, payload);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
