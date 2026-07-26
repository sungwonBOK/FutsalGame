public sealed class PossessionInputContext
{
    public const float SprintGraceDuration = 0.65f;
    public const float CombatProtectionDuration = 0.40f;

    private float sprintGraceUntil = -999f;
    private float combatProtectionUntil = -999f;
    private float mouseActionBlockUntil = -999f;

    public bool HasPossessionContext { get; private set; }

    public void Update(
        float now,
        bool actuallyHasBall,
        bool opponentHasBall,
        bool withinAcquireRange,
        bool sprintHeld)
    {
        if (actuallyHasBall)
        {
            if (sprintHeld)
                sprintGraceUntil = now + SprintGraceDuration;

            HasPossessionContext = true;
            if (now <= combatProtectionUntil)
                mouseActionBlockUntil = combatProtectionUntil;
            return;
        }

        HasPossessionContext = !opponentHasBall
            && withinAcquireRange
            && sprintHeld
            && now <= sprintGraceUntil;
    }

    public void BeginCombatProtection(float now)
    {
        combatProtectionUntil = now + CombatProtectionDuration;
    }

    public bool AreMouseActionsBlocked(float now) => now < mouseActionBlockUntil;

    public void Clear()
    {
        sprintGraceUntil = -999f;
        combatProtectionUntil = -999f;
        mouseActionBlockUntil = -999f;
        HasPossessionContext = false;
    }
}
