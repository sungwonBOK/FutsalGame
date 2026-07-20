using UnityEngine;

[CreateAssetMenu(menuName = "Futsal Brawl/Characters/Movement Config")]
public class CharacterMovementConfig : ScriptableObject
{
    [Header("Movement Profiles")]
    [SerializeField] private CharacterMovementProfile normal = new CharacterMovementProfile(6f, 45f, 60f, 720f);
    [SerializeField] private CharacterMovementProfile sprint = new CharacterMovementProfile(9f, 60f, 75f, 900f);
    [SerializeField] private CharacterMovementProfile possession = new CharacterMovementProfile(4.8f, 32f, 48f, 540f);

    public CharacterMovementProfile Normal => CharacterMovementUtility.SanitizeProfile(normal, 6f, 720f);
    public CharacterMovementProfile Sprint => CharacterMovementUtility.SanitizeProfile(sprint, Normal.speed, Normal.rotationSpeed);
    public CharacterMovementProfile Possession => CharacterMovementUtility.SanitizeProfile(possession, Normal.speed, Normal.rotationSpeed);

    public CharacterMovementProfile ResolveProfile(bool sprint, bool hasBall)
    {
        if (hasBall)
            return Possession;
        return sprint ? Sprint : Normal;
    }
}
