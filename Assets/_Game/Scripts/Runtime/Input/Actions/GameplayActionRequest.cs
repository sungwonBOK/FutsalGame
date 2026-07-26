public readonly struct GameplayActionRequest
{
    public static GameplayActionRequest None => new GameplayActionRequest(GameplayActionId.None);

    public GameplayActionRequest(GameplayActionId id)
    {
        Id = id;
    }

    public GameplayActionId Id { get; }
}
