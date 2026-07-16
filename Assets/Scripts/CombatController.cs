using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 공용 전투: 펀치 / 슬라이딩 태클. 키 입력을 직접 읽지 않고 Punch()/SlideTackle()을 호출받는다.
/// 명중 시 상대를 기절+넉백시키고, 상대가 공을 소유 중이면 공을 튕겨낸다.
/// 플레이어와 상대(더미/AI)가 동일하게 사용한다.
/// </summary>
[RequireComponent(typeof(CharacterState))]
[RequireComponent(typeof(CharacterMotor))]
public class CombatController : MonoBehaviour
{
    [Header("Punch (J)")]
    [Tooltip("펀치 판정 중심까지의 앞쪽 거리.")]
    [SerializeField] private float punchRange = 1.3f;
    [Tooltip("펀치 판정 반경.")]
    [SerializeField] private float punchRadius = 0.7f;
    [Tooltip("펀치 쿨다운(초).")]
    [SerializeField] private float punchCooldown = 0.5f;

    [Header("Slide Tackle (K)")]
    [Tooltip("슬라이딩 대시 속도.")]
    [SerializeField] private float slideSpeed = 12f;
    [Tooltip("슬라이딩 지속 시간(초).")]
    [SerializeField] private float slideDuration = 0.35f;
    [Tooltip("슬라이딩 쿨다운(초).")]
    [SerializeField] private float slideCooldown = 0.8f;
    [Tooltip("슬라이딩 중 명중 판정 반경.")]
    [SerializeField] private float slideHitRadius = 0.8f;

    [Header("Hit Effects (피격자에게 적용)")]
    [Tooltip("넉백 임펄스 힘.")]
    [SerializeField] private float knockbackForce = 8f;
    [Tooltip("기절 시간(초).")]
    [SerializeField] private float stunDuration = 1f;
    [Tooltip("피격자가 소유 중이던 공이 튀어나가는 임펄스 힘.")]
    [SerializeField] private float ballKnockForce = 6f;

    [Header("Effects (연출)")]
    [Tooltip("히트 순간 충격 이펙트 프리합.")]
    [SerializeField] private GameObject hitEffectPrefab;
    [Tooltip("슬라이딩 먼지 이펙트 프리합.")]
    [SerializeField] private GameObject slideDustPrefab;


    private CharacterState state;
    private CharacterMotor motor;    private CharacterAnimator anim;


    private float lastPunchTime = -999f;
    private float lastSlideTime = -999f;
    private float slideActiveUntil = -999f;
    private readonly HashSet<CharacterState> hitThisSlide = new HashSet<CharacterState>();

    private void Awake()
    {
        state = GetComponent<CharacterState>();
        motor = GetComponent<CharacterMotor>();
        anim = GetComponent<CharacterAnimator>();
    }

    /// <summary>펀치 시도. 바라보는 방향 앞 짧은 범위를 순간 판정.</summary>
    public void Punch()
    {
        if (state.IsStunned) return;
        if (Time.time - lastPunchTime < punchCooldown) return;
        lastPunchTime = Time.time;
        if (anim != null) anim.PlayPunch();

        Vector3 center = transform.position + transform.forward * punchRange;
        Collider[] cols = Physics.OverlapSphere(center, punchRadius);
        foreach (var c in cols)
        {
            CharacterState victim = c.GetComponentInParent<CharacterState>();
            if (victim != null && victim != state)
            {
                Hit(victim);
                break; // 한 번에 한 명만
            }
        }
    }

    /// <summary>슬라이딩 태클 시도. 바라보는 방향으로 짧게 돌진하며, 돌진 중 닿으면 명중.</summary>
    public void SlideTackle()
    {
        if (state.IsStunned) return;
        if (Time.time - lastSlideTime < slideCooldown) return;
        lastSlideTime = Time.time;
        if (anim != null) anim.PlaySlide();

        slideActiveUntil = Time.time + slideDuration;
        hitThisSlide.Clear();
        motor.Dash(transform.forward * slideSpeed, slideDuration);

        // 연출: 발밑 먼지 (슬라이드 동안 따라다니게 캐릭터에 부착)
        if (slideDustPrefab != null)
            Instantiate(slideDustPrefab, transform.position + Vector3.down, Quaternion.identity, transform);
    }

    private void FixedUpdate()
    {
        // 슬라이딩 진행 중이면 접촉 판정(한 번의 슬라이드에서 같은 상대는 한 번만).
        if (Time.time < slideActiveUntil)
        {
            Collider[] cols = Physics.OverlapSphere(transform.position, slideHitRadius);
            foreach (var c in cols)
            {
                CharacterState victim = c.GetComponentInParent<CharacterState>();
                if (victim != null && victim != state && !hitThisSlide.Contains(victim))
                {
                    hitThisSlide.Add(victim);
                    Hit(victim);
                }
            }
        }
    }

    /// <summary>명중 처리: (공 소유 중이면 공 튕김) + 넉백 + 기절.</summary>
    private void Hit(CharacterState victim)
    {
        Vector3 dir = victim.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        // 연출: 히트 임팩트(맞은 위치) + 히트 소리
        if (hitEffectPrefab != null)
        {
            Vector3 hitPos = victim.transform.position + Vector3.up * 1f - dir * 0.3f;
            Instantiate(hitEffectPrefab, hitPos, Quaternion.LookRotation(-dir));
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlayHit();

        // 피격자가 공을 소유 중이면 공을 튕겨낸다 (상대 뒤쪽 방향 + 살짝 위로).
        PlayerBallHandler victimBall = victim.GetComponent<PlayerBallHandler>();
        if (victimBall != null && victimBall.HasBall)
        {
            Vector3 ballImpulse = dir * ballKnockForce + Vector3.up * (ballKnockForce * 0.3f);
            victimBall.ForceRelease(ballImpulse);
        }

        // 넘백 + 기절
        victim.ApplyHit(dir * knockbackForce, stunDuration);
    }
}
