using NUnit.Framework;

public class RoomAndSignalingContractTests
{
    [Test]
    public void MpsSessionRoomService_ImplementsRoomServiceBoundary()
    {
        Assert.That(typeof(IRoomService).IsAssignableFrom(typeof(MpsSessionRoomService)), Is.True);
    }

    [Test]
    public void RoomService_DoesNotExposeDedicatedPlayerCountTestRoomActions()
    {
        Assert.That(typeof(IRoomService).GetMethod("CreatePlayerCountTestRoomAsync"), Is.Null);
        Assert.That(typeof(IRoomService).GetMethod("FindPlayerCountTestRoomAsync"), Is.Null);
        Assert.That(typeof(IRoomService).GetMethod("JoinPlayerCountTestRoomAsync"), Is.Null);
    }

    [Test]
    public void LobbySignalRelay_ImplementsPeerSignalingTransportBoundary()
    {
        Assert.That(typeof(IPeerSignalingTransport).IsAssignableFrom(typeof(P2pLobbySignalRelay)), Is.True);
    }
}
