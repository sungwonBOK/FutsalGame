using System.Collections.Generic;
using NUnit.Framework;

public class LobbyTeamJoinPolicyTests
{
    [Test]
    public void FindFirstEmptySlot_UsesTheRequestedTeam()
    {
        List<TeamSlot> slots = new List<TeamSlot>
        {
            new TeamSlot { team = 0, type = Occupant.Empty },
            new TeamSlot { team = 1, type = Occupant.Human, clientId = 7 },
            new TeamSlot { team = 1, type = Occupant.Empty },
        };

        int index = LobbyTeamJoinPolicy.FindFirstEmptySlot(slots, 1);

        Assert.That(index, Is.EqualTo(2));
    }

    [Test]
    public void FindFirstEmptySlot_ReturnsNoneWhenRequestedTeamIsFull()
    {
        List<TeamSlot> slots = new List<TeamSlot>
        {
            new TeamSlot { team = 0, type = Occupant.Empty },
            new TeamSlot { team = 1, type = Occupant.Human, clientId = 7 },
        };

        int index = LobbyTeamJoinPolicy.FindFirstEmptySlot(slots, 1);

        Assert.That(index, Is.EqualTo(-1));
    }
}
