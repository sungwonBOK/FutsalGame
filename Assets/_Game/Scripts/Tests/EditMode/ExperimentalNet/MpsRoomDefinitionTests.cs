using NUnit.Framework;

public class MpsRoomDefinitionTests
{
    [Test]
    public void TryCreate_TrimsNameAndKeepsTheSixPlayerLimit()
    {
        bool created = MpsRoomDefinition.TryCreate(
            "  Friday Futsal  ",
            6,
            true,
            "build-1",
            out MpsRoomDefinition room);

        Assert.That(created, Is.True);
        Assert.That(room.Name, Is.EqualTo("Friday Futsal"));
        Assert.That(room.MaxPlayers, Is.EqualTo(6));
        Assert.That(room.PlayerCount, Is.EqualTo(1));
    }

    [TestCase("", 6, "build-1")]
    [TestCase("   ", 6, "build-1")]
    [TestCase("Friday", 1, "build-1")]
    [TestCase("Friday", 7, "build-1")]
    [TestCase("123456789012345678901234567890123", 6, "build-1")]
    [TestCase("Friday", 6, "")]
    public void TryCreate_RejectsInvalidPublicRoomInputs(string name, int maxPlayers, string buildKey)
    {
        bool created = MpsRoomDefinition.TryCreate(name, maxPlayers, false, buildKey, out _);

        Assert.That(created, Is.False);
    }
}
