using UnityEngine;

/// <summary>
/// 공 소유(Possession) / 드리블 / 슛 처리. 플레이어와 상대(더미/AI)가 동일하게 사용한다.
/// 키 입력을 직접 읽지 않는다 — 슛은 Shoot() 호출로 이뤄진다. (사람=PlayerInput, 이후 상대=AI)
/// 바라보는 방향은 transform.forward(CharacterMotor가 회전)를 사용한다.
/// 한 번에 한 명만 소유하도록 정적 CurrentOwner로 관리한다.
/// </summary>
[RequireComponent(typeof(CharacterState))]
public class PlayerBallHandler : MonoBehaviour
{
    /// <summary>현재 공을 소유한 핸들러. 한 번에 한 명만 소유 가능. null이면 무소유(자유 공).</summary>
    public static PlayerBallHandler CurrentOwner { get; private set; }

    [Header("References")]
    [Tooltip("공의 Rigidbody. 비워두면 이름 'Ball'로 자동 검색한다.")]
    [SerializeField] private Rigidbody ballRb;

    [Header("Possession")]
    [Tooltip("이 거리(평면 기준) 안에 들어오면 공을 자동으로 소유한다.")]
    [SerializeField] private float possessRange = 1.2f;

    [Tooltip("드리블 시 공 위치 오프셋 (로컬 기준: x=우, y=상, z=앞). 앞쪽 약간 앞, 살짝 낮게.")]
    [SerializeField] private Vector3 dribbleOffset = new Vector3(0f, -0.6f, 0.9f);

    [Tooltip("게임 시작 시 이 캐릭터가 공을 소유한 채로 시작한다. (더미 상대 테스트용)")]
    [SerializeField] private bool startWithBall = false;

    [Header("Shooting")]
    [Tooltip("AI(무인자 Shoot)용 기본 슛 임펄스 힘. 사람 플레이어는 아래 차징 값(passForce~maxShootForce)을 사용한다.")]
    [SerializeField] private float shootForce = 6f;

    [Tooltip("슛/피탈 직후 다시 주울 수 없는 쿨다운(초).")]
    [SerializeField] private float shootCooldown = 0.4f;

    [Header("Charged Shot (사람 플레이어)")]
    [Tooltip("탭(짧게 누름) 시 세기. 약한 패스.")]
    [SerializeField] private float passForce = 3.5f;

    [Tooltip("풀차지 시 최대 세기. 강한 슛.")]
    [SerializeField] private float maxShootForce = 13f;

    [Tooltip("풀차지(최대 세기)까지 걸리는 홀드 시간(초).")]
    [SerializeField] private float maxChargeTime = 1f;

    [Header("Effects (연출)")]
    [Tooltip("슈 버스트 이펙트 프리합.")]
    [SerializeField] private GameObject shootEffectPrefab;


    private CharacterState state;
    private Collider ballCollider;    private CharacterAnimator anim;

    private float lastReleaseTime = -999f;

    // 차징(사람 플레이어 전용). AI는 무인자 Shoot()을 쓰므로 차징하지 않는다.
    private bool isCharging;
    private float chargeStartTime;

    /// <summary>이 캐릭터가 현재 공을 소유 중인가.</summary>
    public bool HasBall => CurrentOwner == this;

    /// <summary>현재 차징(스페이스 홀드) 중인가. (게이지 UI가 읽는다.)</summary>
    public bool IsCharging => isCharging;

    /// <summary>현재 차징 정도 0~1. 차징 중이 아니면 0. (게이지 UI가 읽는다.)</summary>
    public float ChargeAmount01 =>
        isCharging ? Mathf.Clamp01((Time.time - chargeStartTime) / Mathf.Max(0.0001f, maxChargeTime)) : 0f;

    private void Awake()
    {
        state = GetComponent<CharacterState>();
        anim = GetComponent<CharacterAnimator>();

        if (ballRb == null)
        {
            GameObject ballGo = GameObject.Find("Ball");
            if (ballGo != null) ballRb = ballGo.GetComponent<Rigidbody>();
        }

        if (ballRb != null)
            ballCollider = ballRb.GetComponent<Collider>();
        else
            Debug.LogWarning("[PlayerBallHandler] Ball Rigidbody를 찾지 못했습니다. Inspector에서 할당하세요.", this);
    }

    private void Start()
    {
        // 시작 시 소유 옵션 (예: 더미 상대가 공을 든 채로 시작).
        if (startWithBall && CurrentOwner == null && ballRb != null)
            Possess();
    }

    private void Update()
    {
        if (ballRb == null) return;

        // 차징 안전 가드: 공을 잃었거나 플레이 정지/기절 상태면 차징을 취소한다(게이지 정리).
        if (isCharging && (!HasBall || !GameManager.PlayActive || (state != null && state.IsStunned)))
            CancelCharge();

        if (!GameManager.PlayActive) return;              // 킥오프 대기 중엔 소유 정지
        if (state != null && state.IsStunned) return;     // 기절 중엔 소유/유지 불가

        // 무소유 상태 + 쿨다운 경과 + 사거리 안이면 자동 소유.
        if (!HasBall && CurrentOwner == null)
        {
            if (Time.time - lastReleaseTime >= shootCooldown && WithinRange())
                Possess();
        }
    }

    private void LateUpdate()
    {
        // 소유 중이면 이동·회전이 끝난 뒤 공을 발 앞에 고정한다.
        // kinematic 추종은 transform을 직접 세팅해야 즉시 반영된다 (Rigidbody.position은 다음 물리 스텝까지 지연됨).
        if (HasBall && ballRb != null)
            ballRb.transform.position = transform.TransformPoint(dribbleOffset);
    }

    private bool WithinRange()
    {
        // 평면(XZ) 거리로 판정 (캐릭터와 공의 높이 차이는 무시).
        Vector3 a = transform.position;
        Vector3 b = ballRb.position;
        a.y = 0f;
        b.y = 0f;
        return (a - b).sqrMagnitude <= possessRange * possessRange;
    }

    private void Possess()
    {
        CurrentOwner = this;
        ballRb.isKinematic = true;                          // 물리 끄기 → 드리블 상태
        if (ballCollider != null) ballCollider.enabled = false; // 소유 중 충돌로 캐릭터를 밀지 않게
    }

    /// <summary>바라보는 방향으로 고정 세기 슛한다. (AI/무인자용, 소유 중일 때만)</summary>
    public void Shoot()
    {
        if (!HasBall) return;
        FireShot(shootForce);
    }

    /// <summary>차징 시작(스페이스 눌림). 공 소유 중일 때만. 이미 차징 중이면 무시.</summary>
    public void StartCharge()
    {
        if (!HasBall || isCharging) return;
        isCharging = true;
        chargeStartTime = Time.time;
    }

    /// <summary>차징 해제(스페이스 뗌). 차징한 시간에 비례한 세기로 슛/패스한다.</summary>
    public void ReleaseCharge()
    {
        if (!isCharging) return;
        float c = ChargeAmount01;
        isCharging = false;

        if (!HasBall) return; // 차징 도중 공을 잃었으면 발사하지 않는다.
        FireShot(Mathf.Lerp(passForce, maxShootForce, c));
    }

    /// <summary>차징을 취소한다(발사하지 않음). 공 상실/기절/플레이 정지 시.</summary>
    public void CancelCharge()
    {
        isCharging = false;
    }

    /// <summary>바라보는 방향으로 지정 세기만큼 공을 찬다(연출/사운드 포함). 소유 중일 때만.</summary>
    private void FireShot(float force)
    {
        if (!HasBall) return;
        if (anim != null) anim.PlayShoot();

        // 연출: 슈 버스트(공 위치) + 슈 소리 (패스/슛 공용)
        if (shootEffectPrefab != null && ballRb != null)
            Instantiate(shootEffectPrefab, ballRb.position, transform.rotation);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayShoot();

        ReleaseWithImpulse(transform.forward * force);
    }

    /// <summary>피격 등으로 공을 강제로 놓게 한다. 지정 임펄스로 공을 튕겨낸다. (소유 중일 때만)</summary>
    public void ForceRelease(Vector3 impulse)
    {
        if (!HasBall) return;
        CancelCharge(); // 태클 등으로 공을 뺏기면 차징도 취소(게이지 정리).
        ReleaseWithImpulse(impulse);
    }

    private void ReleaseWithImpulse(Vector3 impulse)
    {
        // 공을 놓고 물리를 켠 뒤 임펄스를 준다. 이후 shootCooldown 동안은 이 캐릭터가 다시 줍지 못한다.
        CurrentOwner = null;
        lastReleaseTime = Time.time;

        if (ballCollider != null) ballCollider.enabled = true;
        ballRb.isKinematic = false;
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        ballRb.AddForce(impulse, ForceMode.Impulse);
    }

    private void OnDisable()
    {
        // 이 캐릭터가 사라지면 소유권을 반납하고 차징을 취소한다.
        CancelCharge();
        if (CurrentOwner == this)
            CurrentOwner = null;
    }

    /// <summary>현재 소유권을 해제한다(킥오프 리셋용). 공 물리 복구는 호출측(GameManager)에서 처리.</summary>
    public static void ClearPossession()
    {
        CurrentOwner = null;
    }

}
