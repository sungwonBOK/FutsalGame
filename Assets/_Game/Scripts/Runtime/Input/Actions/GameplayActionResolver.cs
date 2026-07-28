public static class GameplayActionResolver
{
    public static GameplayActionRequest Resolve(GameplayActionSlot slot, GameplayActionContext context)
    {
        if (context.MouseActionsBlocked || context.IsCharging)
            return GameplayActionRequest.None;

        if (context.HasPossessionContext)
        {
            return new GameplayActionRequest(slot == GameplayActionSlot.Primary
                ? GameplayActionId.PassCharge
                : GameplayActionId.ShotCharge);
        }

        return new GameplayActionRequest(slot == GameplayActionSlot.Primary
            ? GameplayActionId.BasicPunch
            : GameplayActionId.CrossPunch);
    }
}
