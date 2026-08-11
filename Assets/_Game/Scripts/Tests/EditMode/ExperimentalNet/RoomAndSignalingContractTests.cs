using NUnit.Framework;

public class RoomAndSignalingContractTests
{
    [Test]
    public void MpsSessionRoomService_ImplementsRoomServiceBoundary()
    {
        Assert.That(typeof(IRoomService).IsAssignableFrom(typeof(MpsSessionRoomService)), Is.True);
    }

    [Test]
    public void LobbySignalRelay_ImplementsPeerSignalingTransportBoundary()
    {
        Assert.That(typeof(IPeerSignalingTransport).IsAssignableFrom(typeof(P2pLobbySignalRelay)), Is.True);
    }
}
