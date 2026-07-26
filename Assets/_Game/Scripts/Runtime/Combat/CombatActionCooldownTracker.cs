using System.Collections.Generic;

public sealed class CombatActionCooldownTracker
{
    private readonly Dictionary<CombatActionId, float> lastUseTime = new Dictionary<CombatActionId, float>();

    public bool TryConsume(CombatActionId id, float now, float cooldown)
    {
        if (lastUseTime.TryGetValue(id, out float previous) && now - previous < cooldown)
            return false;

        lastUseTime[id] = now;
        return true;
    }

    public float GetRemaining(CombatActionId id, float now, float cooldown)
    {
        if (!lastUseTime.TryGetValue(id, out float previous))
            return 0f;

        return UnityEngine.Mathf.Max(0f, cooldown - (now - previous));
    }

    public void Clear()
    {
        lastUseTime.Clear();
    }
}
