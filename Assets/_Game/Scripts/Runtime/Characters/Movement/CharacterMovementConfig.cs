using UnityEngine;

[CreateAssetMenu(menuName = "Futsal Brawl/Characters/Movement Config")]
public class CharacterMovementConfig : ScriptableObject
{
    [Header("Movement Profiles")]
    [SerializeField] private CharacterMovementProfile normal = new CharacterMovementProfile(6f, 45f, 60f, 720f);
    [SerializeField] private CharacterMovementProfile sprint = new CharacterMovementProfile(9f, 60f, 75f, 900f);
    [SerializeField] private CharacterMovementProfile possession = new CharacterMovementProfile(4.8f, 32f, 48f, 540f);
    [SerializeField, Min(0f)] private float burstSprintSpeed = 12f;

    [Header("Stamina")]
    [SerializeField, Min(0f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float staminaRegenPerSecond = 22f;
    [SerializeField, Min(0f)] private float staminaRegenDelay = 0.6f;
    [SerializeField, Min(0f)] private float sprintDrainPerSecond = 26f;
    [SerializeField, Min(0f)] private float minStaminaToSprint = 8f;

    [Header("Dodge")]
    [SerializeField, Min(0f)] private float dodgeSpeed = 14f;
    [SerializeField, Min(0f)] private float dodgeDuration = 0.22f;
    [SerializeField, Min(0f)] private float dodgeCost = 30f;
    [SerializeField, Min(0f)] private float dodgeCooldown = 0.9f;
    [SerializeField, Min(0f)] private float dodgeInvulnerability = 0.28f;

    public CharacterMovementProfile Normal => CharacterMovementUtility.SanitizeProfile(normal, 6f, 720f);
    public CharacterMovementProfile Sprint => CharacterMovementUtility.SanitizeProfile(sprint, Normal.speed, Normal.rotationSpeed);
    public CharacterMovementProfile Possession => CharacterMovementUtility.SanitizeProfile(possession, Normal.speed, Normal.rotationSpeed);
    public float BurstSprintSpeed => burstSprintSpeed > 0f ? burstSprintSpeed : 12f;
    // Existing movement assets predate the mobility fields. Keep those assets playable
    // until an editor-side asset migration persists the inspector values.
    public float MaxStamina => maxStamina > 0f ? maxStamina : 100f;
    public float StaminaRegenPerSecond => staminaRegenPerSecond > 0f ? staminaRegenPerSecond : 22f;
    public float StaminaRegenDelay => staminaRegenDelay > 0f ? staminaRegenDelay : 0.6f;
    public float SprintDrainPerSecond => sprintDrainPerSecond > 0f ? sprintDrainPerSecond : 26f;
    public float MinStaminaToSprint => minStaminaToSprint > 0f ? minStaminaToSprint : 8f;
    public float DodgeSpeed => dodgeSpeed > 0f ? dodgeSpeed : 14f;
    public float DodgeDuration => dodgeDuration > 0f ? dodgeDuration : 0.22f;
    public float DodgeCost => dodgeCost > 0f ? dodgeCost : 30f;
    public float DodgeCooldown => dodgeCooldown > 0f ? dodgeCooldown : 0.9f;
    public float DodgeInvulnerability => dodgeInvulnerability > 0f ? dodgeInvulnerability : 0.28f;

    public CharacterMovementProfile ResolveProfile(bool sprint, bool hasBall, bool burstSprint = false)
    {
        CharacterMovementProfile profile = hasBall ? Possession : sprint ? Sprint : Normal;
        if (burstSprint)
            profile.speed = BurstSprintSpeed;
        return profile;
    }
}
