using NUnit.Framework;

public sealed class P2pConnectionFailurePolicyTests
{
    [TestCase(P2pConnectionState.Negotiating, true)]
    [TestCase(P2pConnectionState.Ready, true)]
    [TestCase(P2pConnectionState.Idle, false)]
    [TestCase(P2pConnectionState.Failed, false)]
    [TestCase(P2pConnectionState.Closed, false)]
    public void DataChannelClose_IsTerminalOnlyForAnActiveConnection(P2pConnectionState state, bool expected)
    {
        Assert.That(P2pConnectionFailurePolicy.ShouldFailOnDataChannelClose(state), Is.EqualTo(expected));
    }

    [Test]
    public void FailedOrClosedTransport_IsTerminal()
    {
        Assert.That(P2pConnectionFailurePolicy.ShouldFailOnTransportTerminalState(false, false), Is.False);
        Assert.That(P2pConnectionFailurePolicy.ShouldFailOnTransportTerminalState(true, false), Is.True);
        Assert.That(P2pConnectionFailurePolicy.ShouldFailOnTransportTerminalState(false, true), Is.True);
    }
}
