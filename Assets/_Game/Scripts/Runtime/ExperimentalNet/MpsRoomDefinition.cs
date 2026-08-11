using System;

public sealed class MpsRoomDefinition : IEquatable<MpsRoomDefinition>
{
    public const int MinimumPlayers = 2;
    public const int MaximumPlayers = 6;

    public string Id { get; }
    public string Name { get; }
    public int MaxPlayers { get; }
    public int PlayerCount { get; }
    public bool IsPrivate { get; }
    public string BuildKey { get; }

    private MpsRoomDefinition(string id, string name, int maxPlayers, int playerCount, bool isPrivate, string buildKey)
    {
        Id = id;
        Name = name;
        MaxPlayers = maxPlayers;
        PlayerCount = playerCount;
        IsPrivate = isPrivate;
        BuildKey = buildKey;
    }

    public static bool TryCreate(string name, int maxPlayers, bool isPrivate, string buildKey, out MpsRoomDefinition room)
    {
        string trimmedName = name == null ? string.Empty : name.Trim();
        if (string.IsNullOrEmpty(trimmedName) ||
            trimmedName.Length > 32 ||
            maxPlayers < MinimumPlayers ||
            maxPlayers > MaximumPlayers ||
            string.IsNullOrWhiteSpace(buildKey))
        {
            room = null;
            return false;
        }

        room = new MpsRoomDefinition(string.Empty, trimmedName, maxPlayers, 1, isPrivate, buildKey);
        return true;
    }

    public static MpsRoomDefinition ForRemote(string name, int maxPlayers, int playerCount, bool isPrivate, string buildKey)
    {
        return ForRemote(name, name, maxPlayers, playerCount, isPrivate, buildKey);
    }

    public static MpsRoomDefinition ForRemote(string id, string name, int maxPlayers, int playerCount, bool isPrivate, string buildKey)
    {
        return new MpsRoomDefinition(
            id ?? string.Empty,
            name ?? string.Empty,
            maxPlayers,
            playerCount,
            isPrivate,
            buildKey ?? string.Empty);
    }

    public bool Equals(MpsRoomDefinition other)
    {
        return other != null &&
            Id == other.Id &&
            Name == other.Name &&
            MaxPlayers == other.MaxPlayers &&
            PlayerCount == other.PlayerCount &&
            IsPrivate == other.IsPrivate &&
            BuildKey == other.BuildKey;
    }

    public override bool Equals(object obj) => Equals(obj as MpsRoomDefinition);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Id.GetHashCode();
            hash = (hash * 397) ^ Name.GetHashCode();
            hash = (hash * 397) ^ MaxPlayers;
            hash = (hash * 397) ^ PlayerCount;
            hash = (hash * 397) ^ IsPrivate.GetHashCode();
            return (hash * 397) ^ BuildKey.GetHashCode();
        }
    }
}
