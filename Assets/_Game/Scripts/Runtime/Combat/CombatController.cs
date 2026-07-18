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
    [Tooltip("펀치 쿨다운(초). 기절 시간보다 길어야 무한 스턴락이 성립하지 않는다.")]
    [SerializeField] private float punchCooldown = 1.2f;

    [Header("Slide Tackle (K)")]
    [Tooltip("슬라이딩 대시 속도.")]
    [SerializeField] private float slideSpeed = 12f;
    [Tooltip("슬라이딩 지속 시간(초).")]
    [SerializeField] private float slideDuration = 0.35f;
    [Tooltip("슬라이딩 쿨다운(초). 돌진 이동 + 광역 판정이라는 이중 이득이 있어 길게 잡는다.")]
    [SerializeField] private float slideCooldown = 3f;
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
    private ThirdPersonActionCamera actionCamera;


    private float lastPunchTime = -999f;
    private float lastSlideTime = -999f;
    private float slideActiveUntil = -999f;
    private readonly HashSet<CharacterState> hitThisSlide = new HashSet<CharacterState>();

    // --- 쿨다운 조회용 공개 상태 (쿨다운 UI가 매 프레임 읽는다. 로직은 여기, 표시는 UI.) ---

    /// <summary>펀치 쿨다운 총 길이(초).</summary>
    public float PunchCooldown => punchCooldown;

    /// <summary>슬라이딩 쿨다운 총 길이(초).</summary>
    public float SlideCooldown => slideCooldown;

    /// <summary>펀치 쿨다운 남은 시간(초). 준비 완료면 0.</summary>
    public float PunchRemaining => Mathf.Max(0f, punchCooldown - (Time.time - lastPunchTime));

    /// <summary>슬라이딩 쿨다운 남은 시간(초). 준비 완료면 0.</summary>
    public float SlideRemaining => Mathf.Max(0f, slideCooldown - (Time.time - lastSlideTime));

    /// <summary>펀치 쿨다운 남은 비율 0~1 (1=방금 사용, 0=준비 완료).</summary>
    public float PunchCooldown01 => Mathf.Clamp01(PunchRemaining / Mathf.Max(0.0001f, punchCooldown));

    /// <summary>슬라이딩 쿨다운 남은 비율 0~1 (1=방금 사용, 0=준비 완료).</summary>
    public float SlideCooldown01 => Mathf.Clamp01(SlideRemaining / Mathf.Max(0.0001f, slideCooldown));

    /// <summary>펀치를 지금 쓸 수 있는가(쿨다운 기준).</summary>
    public bool IsPunchReady => PunchRemaining <= 0f;

    /// <summary>슬라이딩을 지금 쓸 수 있는가(쿨다운 기준).</summary>
    public bool IsSlideReady => SlideRemaining <= 0f;

    /// <summary>지금 슬라이딩 돌진이 진행 중인가.</summary>
    public bool IsSliding => Time.time < slideActiveUntil;

    /// <summary>쿨다운 때문에 펀치 입력이 거절된 마지막 시각. UI가 "아직 안 됨" 피드백에 쓴다.</summary>
    public float LastPunchRejectedTime { get; private set; } = -999f;

    /// <summary>쿨다운 때문에 슬라이딩 입력이 거절된 마지막 시각. UI가 "아직 안 됨" 피드백에 쓴다.</summary>
    public float LastSlideRejectedTime { get; private set; } = -999f;

    private void Awake()
    {
        state = GetComponent<CharacterState>();
        motor = GetComponent<CharacterMotor>();
        anim = GetComponent<CharacterAnimator>();

        if (Camera.main != null)
            actionCamera = Camera.main.GetComponent<ThirdPersonActionCamera>();
    }

    /// <summary>펀치 시도. 바라보는 방향 앞 짧은 범위를 순간 판정.</summary>
    public void Punch()
    {
        if (state.IsStunned) return;
        if (Time.time - lastPunchTime < punchCooldown)
        {
            // 쿨다운 때문에 거절됐음을 기록 — UI가 "아직 안 됨"을 보여줄 수 있게 한다.
            LastPunchRejectedTime = Time.time;
            return;
        }
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
        if (Time.time - lastSlideTime < slideCooldown)
        {
            LastSlideRejectedTime = Time.time;
            return;
        }
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
        if (actionCamera != null) actionCamera.PlayHitShake();

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
