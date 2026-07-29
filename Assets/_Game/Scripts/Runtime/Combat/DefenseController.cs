using UnityEngine;

[DisallowMultipleComponent]
public sealed class DefenseController : MonoBehaviour
{
    private CharacterLocomotion locomotion;
    private CharacterAnimator characterAnimator;
    private readonly DefenseWindow defenseWindow = new DefenseWindow();

    public bool IsDefending => defenseWindow.IsActive(Time.time);

    private void Awake()
    {
        locomotion = GetComponent<CharacterLocomotion>();
        characterAnimator = GetComponent<CharacterAnimator>();
    }

    public bool TryStartDefense()
    {
        if (locomotion == null || !locomotion.TrySpendStamina(locomotion.DodgeCost))
            return false;

        defenseWindow.Begin(Time.time);
        return true;
    }

    public bool TryBlockAttack(Vector3 attackerPosition)
    {
        return TryBlock(attackerPosition, false);
    }

    public bool TryBlockTackle(Vector3 attackerPosition)
    {
        return TryBlock(attackerPosition, true);
    }

    private bool TryBlock(Vector3 attackerPosition, bool isTackle)
    {
        if (!defenseWindow.TryBlock(
                Time.time,
                transform.position,
                transform.forward,
                attackerPosition,
                out DefenseBlockDirection direction))
        {
            return false;
        }

        if (isTackle)
            PlayTackleBlock(direction);
        else
            characterAnimator?.PlayBlock(direction);
        return true;
    }

    private void PlayTackleBlock(DefenseBlockDirection direction)
    {
        // Dedicated tackle-block animation and response behavior will replace this fallback.
        characterAnimator?.PlayBlock(direction);
    }
}
