using System;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

public enum P2pConnectionState
{
    Idle,
    Negotiating,
    Ready,
    Failed,
    Closed
}

/// <summary>
/// Owns one direct WebRTC peer connection. Lobby/NGO code only supplies and receives setup messages.
/// </summary>
public sealed class P2pConnectionCoordinator : MonoBehaviour
{
    private const string SnapshotChannelLabel = "futsal-snapshots";
    private const string StunServerUrl = "stun:stun.l.google.com:19302";

    private readonly List<RTCIceCandidateInit> pendingCandidates = new List<RTCIceCandidateInit>();

    private RTCPeerConnection peerConnection;
    private RTCDataChannel snapshotChannel;
    private bool isOfferer;
    private bool hasRemoteDescription;
    private int connectionGeneration;

    public static P2pConnectionCoordinator Current { get; private set; }
    public P2pConnectionState State { get; private set; } = P2pConnectionState.Idle;
    public bool IsReady => State == P2pConnectionState.Ready;

    public event Action<P2pSignalMessage> SignalReady;
    public event Action<P2pConnectionState, string> StateChanged;
    public event Action<byte[]> SnapshotReceived;

    private void Awake()
    {
        Current = this;
    }

    public void Begin(bool shouldCreateOffer)
    {
        ShutdownInternal(false);

        connectionGeneration++;
        isOfferer = shouldCreateOffer;
        hasRemoteDescription = false;
        pendingCandidates.Clear();
        SetState(P2pConnectionState.Negotiating, "Direct P2P connection is being prepared.");

        RTCConfiguration configuration = new RTCConfiguration
        {
            iceServers = new[]
            {
                new RTCIceServer { urls = new[] { StunServerUrl } }
            }
        };

        peerConnection = new RTCPeerConnection(ref configuration);
        peerConnection.OnIceCandidate = SendIceCandidate;
        peerConnection.OnIceConnectionChange = HandleIceConnectionState;
        peerConnection.OnConnectionStateChange = HandlePeerConnectionState;
        peerConnection.OnDataChannel = AttachSnapshotChannel;

        if (!isOfferer)
            return;

        snapshotChannel = peerConnection.CreateDataChannel(
            SnapshotChannelLabel,
            new RTCDataChannelInit { ordered = false, maxRetransmits = 0 });
        AttachSnapshotChannel(snapshotChannel);
        StartCoroutine(CreateAndSendOffer(connectionGeneration));
    }

    public void ReceiveSignal(P2pSignalMessage message)
    {
        if (peerConnection == null)
        {
            Fail("P2P setup was received before the local connection was prepared.");
            return;
        }

        switch (message.Kind)
        {
            case P2pSignalKind.Ready:
                break;

            case P2pSignalKind.Offer:
                if (isOfferer)
                    Fail("Both peers tried to create a P2P offer.");
                else
                    StartCoroutine(ReceiveOfferAndSendAnswer(message.Payload, connectionGeneration));
                break;

            case P2pSignalKind.Answer:
                if (!isOfferer)
                    Fail("A P2P answer was received by the answering peer.");
                else
                    StartCoroutine(ReceiveAnswer(message.Payload, connectionGeneration));
                break;

            case P2pSignalKind.Candidate:
                ReceiveIceCandidate(message.Payload);
                break;
        }
    }

    public bool TrySendSnapshot(byte[] payload)
    {
        if (!IsReady || snapshotChannel == null || snapshotChannel.ReadyState != RTCDataChannelState.Open)
            return false;

        snapshotChannel.Send(payload);
        return true;
    }

    public void Shutdown()
    {
        ShutdownInternal(true);
    }

    private IEnumerator CreateAndSendOffer(int generation)
    {
        RTCSessionDescriptionAsyncOperation createOffer = peerConnection.CreateOffer();
        yield return createOffer;

        if (!IsCurrent(generation) || createOffer.IsError)
        {
            if (IsCurrent(generation))
                Fail("Could not create a direct P2P offer.");
            yield break;
        }

        yield return StartCoroutine(SetLocalDescriptionAndSend(createOffer.Desc, P2pSignalKind.Offer, generation));
    }

    private IEnumerator ReceiveOfferAndSendAnswer(string sdp, int generation)
    {
        RTCSessionDescription offer = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = sdp };
        RTCSetSessionDescriptionAsyncOperation setRemote = peerConnection.SetRemoteDescription(ref offer);
        yield return setRemote;

        if (!IsCurrent(generation) || setRemote.IsError)
        {
            if (IsCurrent(generation))
                Fail("Could not apply the remote P2P offer.");
            yield break;
        }

        hasRemoteDescription = true;
        ApplyPendingCandidates();

        RTCSessionDescriptionAsyncOperation createAnswer = peerConnection.CreateAnswer();
        yield return createAnswer;

        if (!IsCurrent(generation) || createAnswer.IsError)
        {
            if (IsCurrent(generation))
                Fail("Could not create a direct P2P answer.");
            yield break;
        }

        yield return StartCoroutine(SetLocalDescriptionAndSend(createAnswer.Desc, P2pSignalKind.Answer, generation));
    }

    private IEnumerator ReceiveAnswer(string sdp, int generation)
    {
        RTCSessionDescription answer = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = sdp };
        RTCSetSessionDescriptionAsyncOperation setRemote = peerConnection.SetRemoteDescription(ref answer);
        yield return setRemote;

        if (!IsCurrent(generation) || setRemote.IsError)
        {
            if (IsCurrent(generation))
                Fail("Could not apply the remote P2P answer.");
            yield break;
        }

        hasRemoteDescription = true;
        ApplyPendingCandidates();
    }

    private IEnumerator SetLocalDescriptionAndSend(RTCSessionDescription description, P2pSignalKind kind, int generation)
    {
        RTCSetSessionDescriptionAsyncOperation setLocal = peerConnection.SetLocalDescription(ref description);
        yield return setLocal;

        if (!IsCurrent(generation) || setLocal.IsError)
        {
            if (IsCurrent(generation))
                Fail("Could not prepare the local P2P session description.");
            yield break;
        }

        SendSignal(kind, description.sdp);
    }

    private void SendIceCandidate(RTCIceCandidate candidate)
    {
        IceCandidatePayload payload = new IceCandidatePayload
        {
            candidate = candidate.Candidate,
            sdpMid = candidate.SdpMid,
            hasSdpMLineIndex = candidate.SdpMLineIndex.HasValue,
            sdpMLineIndex = candidate.SdpMLineIndex.GetValueOrDefault()
        };

        SendSignal(P2pSignalKind.Candidate, JsonUtility.ToJson(payload));
    }

    private void ReceiveIceCandidate(string json)
    {
        IceCandidatePayload payload = JsonUtility.FromJson<IceCandidatePayload>(json);
        if (payload == null || string.IsNullOrEmpty(payload.candidate))
        {
            Fail("The remote P2P candidate was invalid.");
            return;
        }

        RTCIceCandidateInit candidate = new RTCIceCandidateInit
        {
            candidate = payload.candidate,
            sdpMid = payload.sdpMid,
            sdpMLineIndex = payload.hasSdpMLineIndex ? payload.sdpMLineIndex : (int?)null
        };

        if (!hasRemoteDescription)
        {
            pendingCandidates.Add(candidate);
            return;
        }

        ApplyCandidate(candidate);
    }

    private void ApplyPendingCandidates()
    {
        foreach (RTCIceCandidateInit candidate in pendingCandidates)
            ApplyCandidate(candidate);

        pendingCandidates.Clear();
    }

    private void ApplyCandidate(RTCIceCandidateInit candidateInit)
    {
        using RTCIceCandidate candidate = new RTCIceCandidate(candidateInit);
        if (!peerConnection.AddIceCandidate(candidate))
            Fail("The remote P2P candidate could not be applied.");
    }

    private void AttachSnapshotChannel(RTCDataChannel channel)
    {
        snapshotChannel = channel;
        snapshotChannel.OnOpen = () => SetState(P2pConnectionState.Ready, "Direct P2P connection is ready.");
        snapshotChannel.OnClose = () =>
        {
            if (State == P2pConnectionState.Ready)
                Fail("The direct P2P data channel closed.");
        };
        snapshotChannel.OnMessage = payload => SnapshotReceived?.Invoke(payload);

        if (snapshotChannel.ReadyState == RTCDataChannelState.Open)
            SetState(P2pConnectionState.Ready, "Direct P2P connection is ready.");
    }

    private void HandleIceConnectionState(RTCIceConnectionState iceConnectionState)
    {
        if (iceConnectionState == RTCIceConnectionState.Failed)
            Fail("Direct P2P connectivity could not be established.");
    }

    private void HandlePeerConnectionState(RTCPeerConnectionState peerState)
    {
        if (peerState == RTCPeerConnectionState.Failed)
            Fail("The direct P2P connection failed.");
    }

    private void SendSignal(P2pSignalKind kind, string payload)
    {
        if (!P2pSignalMessage.TryCreate(kind, payload, out P2pSignalMessage message))
        {
            Fail("The P2P setup message was invalid or too large.");
            return;
        }

        SignalReady?.Invoke(message);
    }

    private bool IsCurrent(int generation)
    {
        return generation == connectionGeneration && peerConnection != null && State != P2pConnectionState.Failed;
    }

    private void Fail(string reason)
    {
        if (State == P2pConnectionState.Failed)
            return;

        ShutdownInternal(false);
        SetState(P2pConnectionState.Failed, reason);
    }

    private void ShutdownInternal(bool announceClosed)
    {
        connectionGeneration++;
        pendingCandidates.Clear();

        if (snapshotChannel != null)
        {
            snapshotChannel.OnOpen = null;
            snapshotChannel.OnClose = null;
            snapshotChannel.OnMessage = null;
            snapshotChannel.Dispose();
            snapshotChannel = null;
        }

        if (peerConnection != null)
        {
            peerConnection.OnIceCandidate = null;
            peerConnection.OnIceConnectionChange = null;
            peerConnection.OnConnectionStateChange = null;
            peerConnection.OnDataChannel = null;
            peerConnection.Dispose();
            peerConnection = null;
        }

        if (announceClosed)
            SetState(P2pConnectionState.Closed, "Direct P2P connection closed.");
    }

    private void SetState(P2pConnectionState newState, string message)
    {
        State = newState;
        StateChanged?.Invoke(newState, message);
    }

    private void OnDestroy()
    {
        ShutdownInternal(false);
        if (Current == this)
            Current = null;
    }

    [Serializable]
    private sealed class IceCandidatePayload
    {
        public string candidate;
        public string sdpMid;
        public bool hasSdpMLineIndex;
        public int sdpMLineIndex;
    }
}
