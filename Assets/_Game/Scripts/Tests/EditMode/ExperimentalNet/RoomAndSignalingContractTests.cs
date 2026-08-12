using NUnit.Framework;

public class RoomAndSignalingContractTests
{
    [Test]
    public void MpsSessionRoomService_ImplementsRoomServiceBoundary()
    {
        Assert.That(typeof(IRoomService).IsAssignableFrom(typeof(MpsSessionRoomService)), Is.True);
    }

    [Test]
    public void RoomService_ExposesSharedPlayerCountTestRoomActions()
    {
        Assert.That(typeof(IRoomService).GetMethod("CreatePlayerCountTestRoomAsync"), Is.Not.Null);
        Assert.That(typeof(IRoomService).GetMethod("FindPlayerCountTestRoomAsync"), Is.Not.Null);
        Assert.That(typeof(IRoomService).GetMethod("JoinPlayerCountTestRoomAsync"), Is.Not.Null);
    }

    [Test]
    public void LobbySignalRelay_ImplementsPeerSignalingTransportBoundary()
    {
        Assert.That(typeof(IPeerSignalingTransport).IsAssignableFrom(typeof(P2pLobbySignalRelay)), Is.True);
    }
}
