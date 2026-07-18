using UnityEngine;

/// <summary>
/// 파티클 프리팹에 부착. 재생이 끝나면(지속시간 + 파티클 수명) 자동으로 오브젝트를 삭제한다.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class AutoDestroyParticle : MonoBehaviour
{
    [Tooltip("계산된 재생 시간에 더해줄 여유 시간(초).")]
    [SerializeField] private float extraLifetime = 0.3f;

    private void Start()
    {
        float life = 2f;
        ParticleSystem ps = GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ParticleSystem.MainModule main = ps.main;
            life = main.duration + main.startLifetime.constantMax;
        }
        Destroy(gameObject, life + extraLifetime);
    }
}
