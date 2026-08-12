using System.Threading.Tasks;

/// <summary>
/// Control-plane contract for public room discovery and membership. Gameplay
/// code does not depend on the current MPS implementation behind this boundary.
/// </summary>
public interface IRoomService
{
    Task<MpsRoomDefinition> CreatePublicRoomAsync(string roomName, int maxPlayers);
    Task<MpsRoomDefinition[]> BrowsePublicRoomsAsync();
    Task JoinPublicRoomAsync(MpsRoomDefinition room);
}
