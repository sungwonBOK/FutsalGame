using UnityEngine;

/// <summary>
/// 게임 로직과 Animator를 잇는 브리지. 게임 로직(힘/판정/타이밍)은 건드리지 않고,
/// 상태 값만 읽어 Animator 파라미터를 갱신하거나, 액션 시점에 트리거를 쏜다.
///  - Speed(float)     : Rigidbody 수평 속도 (Idle/Run 전환용) — 매 프레임 폴링
///  - IsStunned(bool)  : CharacterState.IsStunned — 매 프레임 폴링
///  - Shoot/Slide/Punch(trigger) : 액션 스크립트가 PlayShoot/PlaySlide/PlayPunch 호출
/// 플레이어와 AI가 동일하게 사용한다.
/// </summary>
[RequireComponent(typeof(CharacterState))]
public class CharacterAnimator : MonoBehaviour
{
    [Tooltip("대상 Animator. 비우면 자식에서 자동 검색.")]
    [SerializeField] private Animator animator;
    [Tooltip("속도 계산에 쓸 Rigidbody. 비우면 이 오브젝트에서 검색.")]
    [SerializeField] private Rigidbody body;

    private CharacterState state;

    private static readonly int PSpeed = Animator.StringToHash("Speed");
    private static readonly int PShoot = Animator.StringToHash("Shoot");
    private static readonly int PSlide = Animator.StringToHash("Slide");
    private static readonly int PPunch = Animator.StringToHash("Punch");
    private static readonly int PStunned = Animator.StringToHash("IsStunned");

    private void Awake()
    {
        state = GetComponent<CharacterState>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (body == null) body = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (animator == null) return;

        // 수평 속도 → Speed
        float speed = 0f;
        if (body != null)
        {
            Vector3 v = body.linearVelocity;
            v.y = 0f;
            speed = v.magnitude;
        }
        animator.SetFloat(PSpeed, speed);

        // 기절 상태 → IsStunned
        animator.SetBool(PStunned, state != null && state.IsStunned);
    }

    public void PlayShoot() { if (animator != null) animator.SetTrigger(PShoot); }
    public void PlaySlide() { if (animator != null) animator.SetTrigger(PSlide); }
    public void PlayPunch() { if (animator != null) animator.SetTrigger(PPunch); }
}
