using System.Collections.Generic;

public static class MpsRoomBrowserPolicy
{
    public static MpsRoomDefinition[] FilterCompatible(IEnumerable<MpsRoomDefinition> rooms, string buildKey)
    {
        List<MpsRoomDefinition> compatible = new List<MpsRoomDefinition>();
        if (rooms == null || string.IsNullOrWhiteSpace(buildKey))
            return compatible.ToArray();

        foreach (MpsRoomDefinition room in rooms)
        {
            if (room == null ||
                room.IsPrivate ||
                room.MaxPlayers < MpsRoomDefinition.MinimumPlayers ||
                room.MaxPlayers > MpsRoomDefinition.MaximumPlayers ||
                room.PlayerCount >= room.MaxPlayers ||
                room.BuildKey != buildKey)
                continue;

            compatible.Add(room);
        }

        return compatible.ToArray();
    }
}
