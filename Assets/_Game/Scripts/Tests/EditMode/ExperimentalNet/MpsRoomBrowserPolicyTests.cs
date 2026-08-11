using NUnit.Framework;

public class MpsRoomBrowserPolicyTests
{
    [Test]
    public void FilterCompatible_ExcludesPrivateFullAndDifferentBuildRooms()
    {
        MpsRoomDefinition compatible = MpsRoomDefinition.ForRemote("A", 6, 2, false, "build-1");
        MpsRoomDefinition privateRoom = MpsRoomDefinition.ForRemote("B", 6, 1, true, "build-1");
        MpsRoomDefinition full = MpsRoomDefinition.ForRemote("C", 6, 6, false, "build-1");
        MpsRoomDefinition differentBuild = MpsRoomDefinition.ForRemote("D", 6, 1, false, "build-2");

        MpsRoomDefinition[] rooms = MpsRoomBrowserPolicy.FilterCompatible(
            new[] { compatible, privateRoom, full, differentBuild },
            "build-1");

        Assert.That(rooms, Is.EqualTo(new[] { compatible }));
    }
}
