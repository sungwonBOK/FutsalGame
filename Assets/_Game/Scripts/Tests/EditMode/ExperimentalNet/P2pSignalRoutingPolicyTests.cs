using NUnit.Framework;

public class P2pSignalRoutingPolicyTests
{
    [Test]
    public void ClientCanAddressAnotherGuestThroughTheHostControlPlane()
    {
        Assert.That(
            P2pSignalRoutingPolicy.CanSendToRecipient(
                isServer: false,
                localClientId: 2,
                serverClientId: 0,
                recipientClientId: 3),
            Is.True);
    }

    [Test]
    public void ClientCannotAddressItself()
    {
        Assert.That(
            P2pSignalRoutingPolicy.CanSendToRecipient(
                isServer: false,
                localClientId: 2,
                serverClientId: 0,
                recipientClientId: 2),
            Is.False);
    }
}
