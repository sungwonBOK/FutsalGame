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
    private const string CombatChannelLabel = "futsal-combat";
    private const string BallStateChannelLabel = "futsal-ball-state";
    private const string BallEventChannelLabel = "futsal-ball-events";
    private static readonly P2pGameplayReadiness gameplayReadiness = new P2pGameplayReadiness(
        P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat | P2pGameplayChannel.Ball);
    private const string StunServerUrl = "stun:stun.l.google.com:19302";

    private readonly List<RTCIceCandidateInit> pendingCandidates = new List<RTCIceCandidateInit>();

    private RTCPeerConnection peerConnection;
    private RTCDataChannel snapshotChannel;
    private RTCDataChannel combatChannel;
    private RTCDataChannel ballStateChannel;
    private RTCDataChannel ballEventChannel;
    private bool isOfferer;
    private bool hasRemoteDescription;
    private int connectionGeneration;
    private int generatedCandidateCount;
    private int receivedCandidateCount;
    private int appliedCandidateCount;

    /// <summary>The NGO client ID of the one remote peer owned by this coordinator.</summary>
    public ulong RemoteClientId { get; private set; }
    public P2pConnectionState State { get; private set; } = P2pConnectionState.Idle;
    public bool IsReady => State == P2pConnectionState.Ready;
    public bool IsCombatReady => IsReady && combatChannel != null && combatChannel.ReadyState == RTCDataChannelState.Open;
    public bool IsBallReady => IsReady
        && ballStateChannel != null && ballStateChannel.ReadyState == RTCDataChannelState.Open
        && ballEventChannel != null && ballEventChannel.ReadyState == RTCDataChannelState.Open;
    public P2pGameplayChannel OpenGameplayChannels
    {
        get
        {
            P2pGameplayChannel channels = P2pGameplayChannel.None;
            if (IsReady && snapshotChannel != null && snapshotChannel.ReadyState == RTCDataChannelState.Open)
                channels |= P2pGameplayChannel.Snapshot;
            if (IsCombatReady)
                channels |= P2pGameplayChannel.Combat;
            if (IsBallReady)
                channels |= P2pGameplayChannel.Ball;
            return channels;
        }
    }
    public bool IsGameplayReady => gameplayReadiness.IsReady(OpenGameplayChannels);

    public event Action<P2pSignalMessage> SignalReady;
    public event Action<P2pConnectionState, string> StateChanged;
    public event Action<P2pGameplayChannel> GameplayChannelsChanged;
    public event Action<byte[]> SnapshotReceived;
    public event Action<byte[]> CombatReceived;
    public event Action<byte[]> BallStateReceived;
    public event Action<byte[]> BallEventReceived;

    /// <summary>
    /// Assigns this component to one remote peer before negotiation begins. A
    /// mesh registry owns component creation and never reuses an active
    /// coordinator for another peer.
    /// </summary>
    public void ConfigureRemotePeer(ulong remoteClientId)
    {
        if (peerConnection != null)
            throw new InvalidOperationException("A P2P coordinator cannot change remote peers while connected.");

        RemoteClientId = remoteClientId;
    }

    public void Begin(bool shouldCreateOffer)
    {
        ShutdownInternal(false);

        connectionGeneration++;
        isOfferer = shouldCreateOffer;
        hasRemoteDescription = false;
        pendingCandidates.Clear();
        generatedCandidateCount = 0;
        receivedCandidateCount = 0;
        appliedCandidateCount = 0;
        SetState(P2pConnectionState.Negotiating, "Direct P2P connection is being prepared.");
        Debug.Log(P2pDiagnosticFormatter.ConnectionPrepared(isOfferer, connectionGeneration), this);

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
        peerConnection.OnDataChannel = AttachDataChannel;

        if (!isOfferer)
            return;

        snapshotChannel = peerConnection.CreateDataChannel(
            SnapshotChannelLabel,
            new RTCDataChannelInit { ordered = false, maxRetransmits = 0 });
        AttachSnapshotChannel(snapshotChannel);
        combatChannel = peerConnection.CreateDataChannel(
            CombatChannelLabel,
            new RTCDataChannelInit { ordered = true });
        AttachCombatChannel(combatChannel);
        ballStateChannel = peerConnection.CreateDataChannel(
            BallStateChannelLabel,
            new RTCDataChannelInit { ordered = false, maxRetransmits = 0 });
        AttachBallStateChannel(ballStateChannel);
        ballEventChannel = peerConnection.CreateDataChannel(
            BallEventChannelLabel,
            new RTCDataChannelInit { ordered = true });
        AttachBallEventChannel(ballEventChannel);
        StartCoroutine(CreateAndSendOffer(connectionGeneration));
    }

    public void ReceiveSignal(P2pSignalMessage message)
    {
        Debug.Log(P2pDiagnosticFormatter.Signal(isOfferer, "received", message.Kind, message.Payload.Length), this);

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

    public bool TrySendCombat(byte[] payload)
    {
        if (!IsCombatReady)
            return false;

        combatChannel.Send(payload);
        return true;
    }

    public bool TrySendBallState(byte[] payload)
    {
        if (!IsBallReady)
            return false;

        ballStateChannel.Send(payload);
        return true;
    }

    public bool TrySendBallEvent(byte[] payload)
    {
        if (!IsBallReady)
            return false;

        ballEventChannel.Send(payload);
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
                FailOperation("creating local offer", "Could not create a direct P2P offer.", createOffer.Error);
            yield break;
        }

        yield return StartCoroutine(SetLocalDescriptionAndSend(createOffer.Desc, P2pSignalKind.Offer, generation));
    }

    private IEnumerator ReceiveOfferAndSendAnswer(string sdp, int generation)
    {
        RTCSessionDescription offer = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = sdp };
        RTCSetSessionDescriptionAsyncOperation setRemote;
        try
        {
            setRemote = peerConnection.SetRemoteDescription(ref offer);
        }
        catch (Exception exception)
        {
            FailOperation("applying remote offer", "Could not apply the remote P2P offer.", exception);
            yield break;
        }

        yield return setRemote;

        if (!IsCurrent(generation) || setRemote.IsError)
        {
            if (IsCurrent(generation))
                FailOperation("applying remote offer", "Could not apply the remote P2P offer.", setRemote.Error);
            yield break;
        }

        hasRemoteDescription = true;
        ApplyPendingCandidates();

        RTCSessionDescriptionAsyncOperation createAnswer = peerConnection.CreateAnswer();
        yield return createAnswer;

        if (!IsCurrent(generation) || createAnswer.IsError)
        {
            if (IsCurrent(generation))
                FailOperation("creating local answer", "Could not create a direct P2P answer.", createAnswer.Error);
            yield break;
        }

        yield return StartCoroutine(SetLocalDescriptionAndSend(createAnswer.Desc, P2pSignalKind.Answer, generation));
    }

    private IEnumerator ReceiveAnswer(string sdp, int generation)
    {
        RTCSessionDescription answer = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = sdp };
        RTCSetSessionDescriptionAsyncOperation setRemote;
        try
        {
            setRemote = peerConnection.SetRemoteDescription(ref answer);
        }
        catch (Exception exception)
        {
            FailOperation("applying remote answer", "Could not apply the remote P2P answer.", exception);
            yield break;
        }

        yield return setRemote;

        if (!IsCurrent(generation) || setRemote.IsError)
        {
            if (IsCurrent(generation))
                FailOperation("applying remote answer", "Could not apply the remote P2P answer.", setRemote.Error);
            yield break;
        }

        hasRemoteDescription = true;
        ApplyPendingCandidates();
    }

    private IEnumerator SetLocalDescriptionAndSend(RTCSessionDescription description, P2pSignalKind kind, int generation)
    {
        RTCSetSessionDescriptionAsyncOperation setLocal;
        string operation = kind == P2pSignalKind.Offer ? "applying local offer" : "applying local answer";
        try
        {
            setLocal = peerConnection.SetLocalDescription(ref description);
        }
        catch (Exception exception)
        {
            FailOperation(operation, "Could not prepare the local P2P session description.", exception);
            yield break;
        }

        yield return setLocal;

        if (!IsCurrent(generation) || setLocal.IsError)
        {
            if (IsCurrent(generation))
                FailOperation(operation, "Could not prepare the local P2P session description.", setLocal.Error);
            yield break;
        }

        SendSignal(kind, description.sdp);
    }

    private void SendIceCandidate(RTCIceCandidate candidate)
    {
        generatedCandidateCount++;
        Debug.Log(P2pDiagnosticFormatter.Candidate(isOfferer, "generated", generatedCandidateCount, pendingCandidates.Count), this);

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
            FailOperation("processing remote candidate", "The remote P2P candidate was invalid.", "InvalidCandidate", "The candidate payload was empty.");
            return;
        }

        RTCIceCandidateInit candidate = new RTCIceCandidateInit
        {
            candidate = payload.candidate,
            sdpMid = payload.sdpMid,
            sdpMLineIndex = payload.hasSdpMLineIndex ? payload.sdpMLineIndex : (int?)null
        };

        receivedCandidateCount++;

        if (!hasRemoteDescription)
        {
            pendingCandidates.Add(candidate);
            Debug.Log(P2pDiagnosticFormatter.Candidate(isOfferer, "queued", receivedCandidateCount, pendingCandidates.Count), this);
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
        {
            FailOperation("applying remote candidate", "The remote P2P candidate could not be applied.", "AddIceCandidate", "The WebRTC peer rejected the candidate.");
            return;
        }

        appliedCandidateCount++;
        Debug.Log(P2pDiagnosticFormatter.Candidate(isOfferer, "applied", appliedCandidateCount, pendingCandidates.Count), this);
    }

    private void AttachDataChannel(RTCDataChannel channel)
    {
        if (channel.Label == CombatChannelLabel)
            AttachCombatChannel(channel);
        else if (channel.Label == BallStateChannelLabel)
            AttachBallStateChannel(channel);
        else if (channel.Label == BallEventChannelLabel)
            AttachBallEventChannel(channel);
        else
            AttachSnapshotChannel(channel);
    }

    private void AttachSnapshotChannel(RTCDataChannel channel)
    {
        snapshotChannel = channel;
        snapshotChannel.OnOpen = () =>
        {
            Debug.Log(P2pDiagnosticFormatter.DataChannel(isOfferer, "opened"), this);
            SetState(P2pConnectionState.Ready, "Direct P2P connection is ready.");
            NotifyGameplayChannelsChanged();
        };
        snapshotChannel.OnClose = () =>
        {
            Debug.Log(P2pDiagnosticFormatter.DataChannel(isOfferer, "closed"), this);
            if (P2pConnectionFailurePolicy.ShouldFailOnDataChannelClose(State))
                Fail("The direct P2P data channel closed.");
        };
        snapshotChannel.OnMessage = payload => SnapshotReceived?.Invoke(payload);

        if (snapshotChannel.ReadyState == RTCDataChannelState.Open)
        {
            SetState(P2pConnectionState.Ready, "Direct P2P connection is ready.");
            NotifyGameplayChannelsChanged();
        }
    }

    private void HandleIceConnectionState(RTCIceConnectionState iceConnectionState)
    {
        Debug.Log(P2pDiagnosticFormatter.IceState(
            isOfferer,
            iceConnectionState.ToString(),
            generatedCandidateCount,
            receivedCandidateCount,
            appliedCandidateCount,
            pendingCandidates.Count), this);

        if (P2pConnectionFailurePolicy.ShouldFailOnTransportTerminalState(
                iceConnectionState == RTCIceConnectionState.Failed,
                iceConnectionState == RTCIceConnectionState.Closed))
            Fail("Direct P2P connectivity could not be established.");
    }

    private void AttachCombatChannel(RTCDataChannel channel)
    {
        combatChannel = channel;
        combatChannel.OnOpen = NotifyGameplayChannelsChanged;
        combatChannel.OnClose = () =>
        {
            if (P2pConnectionFailurePolicy.ShouldFailOnDataChannelClose(State))
                Fail("The direct P2P combat channel closed.");
        };
        combatChannel.OnMessage = payload => CombatReceived?.Invoke(payload);
        NotifyGameplayChannelsChanged();
    }

    private void AttachBallStateChannel(RTCDataChannel channel)
    {
        ballStateChannel = channel;
        ballStateChannel.OnOpen = NotifyGameplayChannelsChanged;
        ballStateChannel.OnClose = () =>
        {
            if (P2pConnectionFailurePolicy.ShouldFailOnDataChannelClose(State))
                Fail("The direct P2P ball state channel closed.");
        };
        ballStateChannel.OnMessage = payload => BallStateReceived?.Invoke(payload);
        NotifyGameplayChannelsChanged();
    }

    private void AttachBallEventChannel(RTCDataChannel channel)
    {
        ballEventChannel = channel;
        ballEventChannel.OnOpen = NotifyGameplayChannelsChanged;
        ballEventChannel.OnClose = () =>
        {
            if (P2pConnectionFailurePolicy.ShouldFailOnDataChannelClose(State))
                Fail("The direct P2P ball event channel closed.");
        };
        ballEventChannel.OnMessage = payload => BallEventReceived?.Invoke(payload);
        NotifyGameplayChannelsChanged();
    }

    private void HandlePeerConnectionState(RTCPeerConnectionState peerState)
    {
        Debug.Log(P2pDiagnosticFormatter.PeerState(isOfferer, peerState.ToString()), this);

        if (P2pConnectionFailurePolicy.ShouldFailOnTransportTerminalState(
                peerState == RTCPeerConnectionState.Failed,
                peerState == RTCPeerConnectionState.Closed))
            Fail("The direct P2P connection failed.");
    }

    private void SendSignal(P2pSignalKind kind, string payload)
    {
        if (!P2pSignalMessage.TryCreate(kind, payload, out P2pSignalMessage message))
        {
            Fail("The P2P setup message was invalid or too large.");
            return;
        }

        Debug.Log(P2pDiagnosticFormatter.Signal(isOfferer, "sent", kind, payload.Length), this);
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

    private void FailOperation(string operation, string reason, RTCError error)
    {
        FailOperation(operation, reason, error.errorType.ToString(), error.message);
    }

    private void FailOperation(string operation, string reason, Exception exception)
    {
        FailOperation(operation, reason, exception.GetType().Name, exception.Message);
    }

    private void FailOperation(string operation, string reason, string errorType, string errorMessage)
    {
        Debug.LogError(P2pDiagnosticFormatter.OperationFailure(isOfferer, operation, errorType, errorMessage), this);
        Fail(reason);
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

        if (combatChannel != null)
        {
            combatChannel.OnOpen = null;
            combatChannel.OnClose = null;
            combatChannel.OnMessage = null;
            combatChannel.Dispose();
            combatChannel = null;
        }

        if (ballStateChannel != null)
        {
            ballStateChannel.OnOpen = null;
            ballStateChannel.OnClose = null;
            ballStateChannel.OnMessage = null;
            ballStateChannel.Dispose();
            ballStateChannel = null;
        }

        if (ballEventChannel != null)
        {
            ballEventChannel.OnOpen = null;
            ballEventChannel.OnClose = null;
            ballEventChannel.OnMessage = null;
            ballEventChannel.Dispose();
            ballEventChannel = null;
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

    private void NotifyGameplayChannelsChanged()
    {
        GameplayChannelsChanged?.Invoke(OpenGameplayChannels);
    }

    private void OnDestroy()
    {
        ShutdownInternal(false);
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
