using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;

public sealed class MpsSessionRoomService
{
    public const string BuildPropertyKey = "build";

    private readonly string buildKey;

    public MpsSessionRoomService(string buildKey)
    {
        if (string.IsNullOrWhiteSpace(buildKey))
            throw new ArgumentException("A build key is required.", nameof(buildKey));

        this.buildKey = buildKey;
    }

    public async Task<MpsRoomDefinition> CreatePublicRoomAsync(string roomName, int maxPlayers)
    {
        if (!MpsRoomDefinition.TryCreate(roomName, maxPlayers, false, buildKey, out MpsRoomDefinition requestedRoom))
            throw new ArgumentException("The room name or player limit is invalid.");

        await RelayConnectionService.InitializeAsync();

        SessionOptions options = new SessionOptions
        {
            Name = requestedRoom.Name,
            MaxPlayers = requestedRoom.MaxPlayers,
            IsPrivate = false,
            IsLocked = false,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                {
                    BuildPropertyKey,
                    new SessionProperty(buildKey, VisibilityPropertyOptions.Public, PropertyIndex.String1)
                }
            }
        }.WithRelayNetwork(new RelayNetworkOptions());

        IHostSession session = await MultiplayerService.Instance.CreateSessionAsync(options);
        return MpsRoomDefinition.ForRemote(
            session.Id,
            session.Name,
            session.MaxPlayers,
            session.PlayerCount,
            session.IsPrivate,
            buildKey);
    }

    public async Task<MpsRoomDefinition[]> BrowsePublicRoomsAsync()
    {
        await RelayConnectionService.InitializeAsync();

        QuerySessionsResults query = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions
        {
            Count = 25,
            FilterOptions = new List<FilterOption>
            {
                new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater),
                new FilterOption(FilterField.StringIndex1, buildKey, FilterOperation.Equal)
            },
            SortOptions = new List<SortOption>
            {
                new SortOption(SortOrder.Descending, SortField.LastUpdated)
            }
        });

        List<MpsRoomDefinition> rooms = new List<MpsRoomDefinition>(query.Sessions.Count);
        foreach (ISessionInfo session in query.Sessions)
        {
            string roomBuildKey = session.Properties.TryGetValue(BuildPropertyKey, out SessionProperty buildProperty)
                ? buildProperty.Value
                : string.Empty;

            rooms.Add(MpsRoomDefinition.ForRemote(
                session.Id,
                session.Name,
                session.MaxPlayers,
                session.MaxPlayers - session.AvailableSlots,
                false,
                roomBuildKey));
        }

        return MpsRoomBrowserPolicy.FilterCompatible(rooms, buildKey);
    }

    public async Task JoinPublicRoomAsync(MpsRoomDefinition room)
    {
        if (room == null || string.IsNullOrWhiteSpace(room.Id))
            throw new ArgumentException("A session id is required.", nameof(room));

        await RelayConnectionService.InitializeAsync();
        await MultiplayerService.Instance.JoinSessionByIdAsync(room.Id);
    }
}
