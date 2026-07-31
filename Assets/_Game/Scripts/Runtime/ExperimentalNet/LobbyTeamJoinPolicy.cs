using System.Collections.Generic;

public static class LobbyTeamJoinPolicy
{
    public static int FindFirstEmptySlot(IReadOnlyList<TeamSlot> slots, byte team)
    {
        if (slots == null || team > 1)
            return -1;

        for (int index = 0; index < slots.Count; index++)
        {
            TeamSlot slot = slots[index];
            if (slot.team == team && slot.type == Occupant.Empty)
                return index;
        }

        return -1;
    }
}
