using System;

/// <summary>
/// Sends addressed WebRTC setup signals through a low-frequency control plane.
/// It deliberately exposes no gameplay packet API so future transports can
/// replace NGO/MPS signaling without changing P2P gameplay consumers.
/// </summary>
public interface IPeerSignalingTransport
{
    event Action<P2pPeerSignal> SignalReceived;

    void Start();
    void Stop();
    bool TrySend(P2pPeerSignal peerSignal, out string error);
}
