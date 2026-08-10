using UnityEngine;

/// <summary>
/// 게임 로직과 Animator를 잇는 브리지. 게임 로직(힘/판정/타이밍)은 건드리지 않고,
/// 상태 값만 읽어 Animator 파라미터를 갱신하거나, 액션 시점에 트리거를 쏜다.
///  - Speed(float)     : 실제 수평 이동 속도 (Idle/Run 전환용) — 매 프레임 폴링
///  - IsStunned(bool)  : CharacterState.IsStunned — 매 프레임 폴링
///  - Shoot/Slide/Punch(trigger) : 액션 스크립트가 PlayShoot/PlaySlide/PlayPunch 호출
/// 플레이어와 AI가 동일하게 사용한다.
///
/// 속도는 Rigidbody가 아니라 실제 위치 변화에서 구한다. 온라인 경기에서 다른 사람의 캐릭터는
/// 물리로 움직이지 않고 네트워크로 위치만 복제받기 때문에(Rigidbody 속도가 0),
/// 위치 변화를 봐야 원격 캐릭터도 달리는 모션이 나온다.
/// </summary>
[RequireComponent(typeof(CharacterState))]
public class CharacterAnimator : MonoBehaviour
{
    [Tooltip("대상 Animator. 비우면 자식에서 자동 검색.")]
    [SerializeField] private Animator animator;
    [Tooltip("속도 값 평활화 정도(초). 클수록 부드럽지만 반응이 느리다.")]
    [SerializeField] private float speedSmoothing = 0.08f;

    /// <summary>한 프레임 이동량이 이 값을 넘으면 순간이동으로 본다(제곱 거리).</summary>
    private const float TeleportSqrThreshold = 4f;

    private CharacterState state;
    private CharacterLocomotion locomotion;
    private Vector3 lastPosition;
    private float smoothedSpeed;
    private Transform visualRoot;
    private Transform hips;
    private Vector3 visualRootRestLocalPosition;
    private float hipsRestLocalY;
    private bool hasGrabPoseReference;

    private static readonly int PSpeed = Animator.StringToHash("Speed");
    private static readonly int PShoot = Animator.StringToHash("Shoot");
    private static readonly int PSlide = Animator.StringToHash("Slide");
    private static readonly int PPunch = Animator.StringToHash("Punch");
    private static readonly int PCrossPunch = Animator.StringToHash("CrossPunch");
    private static readonly int PGrabStart = Animator.StringToHash("GrabStart");
    private static readonly int PGrabRelease = Animator.StringToHash("GrabRelease");
    private static readonly int PLeftBlock = Animator.StringToHash("LeftBlock");
    private static readonly int PRightBlock = Animator.StringToHash("RightBlock");
    private static readonly int PBackBlock = Animator.StringToHash("BackBlock");
    private static readonly int PStunned = Animator.StringToHash("IsStunned");
    private static readonly int PIdle = Animator.StringToHash("Base Layer.Idle");

    private void Awake()
    {
        state = GetComponent<CharacterState>();
        locomotion = GetComponent<CharacterLocomotion>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        lastPosition = transform.position;
        visualRoot = animator != null ? animator.transform : null;
        hips = FindHips(visualRoot);
    }

    private void OnEnable()
    {
        // 순간이동(스폰/킥오프 리셋) 직후 속도가 튀지 않도록 기준점을 다시 잡는다.
        lastPosition = transform.position;
        smoothedSpeed = 0f;
    }

    private void Update()
    {
        if (animator == null) return;

        animator.SetFloat(PSpeed, MeasureSpeed());
        animator.speed = locomotion != null && locomotion.IsBurstSprinting ? 1.4f : 1f;

        // 기절 상태 → IsStunned
        animator.SetBool(PStunned, state != null && state.IsStunned);
    }

    /// <summary>실제로 움직인 거리로 수평 속도를 구한다(로컬·원격 캐릭터 모두 동일하게 동작).</summary>
    private float MeasureSpeed()
    {
        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        lastPosition = position;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return smoothedSpeed; // 일시정지(timeScale 0) 중에는 값을 유지

        delta.y = 0f;
        float rawSpeed = delta.magnitude / deltaTime;

        // 리셋 등으로 한 프레임에 크게 순간이동하면 속도로 치지 않는다.
        if (delta.sqrMagnitude > TeleportSqrThreshold)
            rawSpeed = 0f;

        smoothedSpeed = speedSmoothing > 0f
            ? Mathf.Lerp(smoothedSpeed, rawSpeed, deltaTime / speedSmoothing)
            : rawSpeed;

        return smoothedSpeed;
    }

    private void LateUpdate()
    {
        if (visualRoot == null || hips == null)
            return;

        if (!IsGrabAnimationActive())
        {
            if (hasGrabPoseReference)
                visualRoot.localPosition = visualRootRestLocalPosition;

            visualRootRestLocalPosition = visualRoot.localPosition;
            hipsRestLocalY = hips.localPosition.y;
            hasGrabPoseReference = true;
            return;
        }

        if (!hasGrabPoseReference)
            return;

        float verticalOffset = CalculateGrabVerticalOffset(hipsRestLocalY, hips.localPosition.y);
        visualRoot.localPosition = visualRootRestLocalPosition + Vector3.up * verticalOffset;
    }

    public void PlayShoot() { if (animator != null) animator.SetTrigger(PShoot); }
    public void PlaySlide() { if (animator != null) animator.SetTrigger(PSlide); }
    public void PlayPunch() { if (animator != null) animator.SetTrigger(PPunch); }
    public void PlayCrossPunch() { if (animator != null) animator.SetTrigger(PCrossPunch); }
    public void PlayGrabStart() { if (animator != null) animator.SetTrigger(PGrabStart); }
    public void PlayGrabRelease() { if (animator != null) animator.SetTrigger(PGrabRelease); }
    public void PlayBlock(DefenseBlockDirection direction)
    {
        if (animator == null)
            return;

        switch (direction)
        {
            case DefenseBlockDirection.Left:
                animator.SetTrigger(PLeftBlock);
                break;
            case DefenseBlockDirection.Back:
                animator.SetTrigger(PBackBlock);
                break;
            default:
                animator.SetTrigger(PRightBlock);
                break;
        }
    }

    public void PlayP2pPresentation(P2pPresentationAction action, float clipStartOffset, DefenseBlockDirection blockDirection)
    {
        if (animator == null)
            return;

        if (clipStartOffset <= 0f || !TryGetStateHash(action, blockDirection, out int stateHash))
        {
            PlayP2pTrigger(action, blockDirection);
            return;
        }

        AnimationClip clip = FindClip(action, blockDirection);
        if (clip == null || clip.length <= 0f)
        {
            PlayP2pTrigger(action, blockDirection);
            return;
        }

        animator.Play(stateHash, 0, Mathf.Clamp01(clipStartOffset / clip.length));
    }

    public void CancelP2pPresentation(P2pPresentationCancelStyle cancelStyle)
    {
        if (animator == null)
            return;

        animator.ResetTrigger(PShoot);
        animator.ResetTrigger(PSlide);
        animator.ResetTrigger(PPunch);
        animator.ResetTrigger(PCrossPunch);
        animator.ResetTrigger(PGrabStart);
        animator.CrossFade(PIdle, cancelStyle == P2pPresentationCancelStyle.Immediate ? 0f : 0.08f);
    }

    public static float CalculateGrabVerticalOffset(float baselineHipLocalY, float currentHipLocalY)
    {
        return baselineHipLocalY - currentHipLocalY;
    }

    private bool IsGrabAnimationActive()
    {
        if (animator == null)
            return false;

        if (IsGrabState(animator.GetCurrentAnimatorStateInfo(0)))
            return true;

        return animator.IsInTransition(0) && IsGrabState(animator.GetNextAnimatorStateInfo(0));
    }

    private static bool IsGrabState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName("Base Layer.GrabStart")
            || stateInfo.IsName("Base Layer.GrabHold")
            || stateInfo.IsName("Base Layer.GrabRelease");
    }

    private static Transform FindHips(Transform root)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child.name.IndexOf("Hips", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return child;
        }

        return null;
    }

    private void PlayP2pTrigger(P2pPresentationAction action, DefenseBlockDirection blockDirection)
    {
        switch (action)
        {
            case P2pPresentationAction.Punch: PlayPunch(); break;
            case P2pPresentationAction.CrossPunch: PlayCrossPunch(); break;
            case P2pPresentationAction.Tackle: PlaySlide(); break;
            case P2pPresentationAction.Grab: PlayGrabStart(); break;
            case P2pPresentationAction.Block: PlayBlock(blockDirection); break;
            case P2pPresentationAction.Pass:
            case P2pPresentationAction.Shot:
                PlayShoot();
                break;
        }
    }

    private static bool TryGetStateHash(P2pPresentationAction action, DefenseBlockDirection blockDirection, out int stateHash)
    {
        string stateName;
        switch (action)
        {
            case P2pPresentationAction.Punch: stateName = "Punch"; break;
            case P2pPresentationAction.CrossPunch: stateName = "CrossPunch"; break;
            case P2pPresentationAction.Tackle: stateName = "Slide"; break;
            case P2pPresentationAction.Grab: stateName = "GrabStart"; break;
            case P2pPresentationAction.Block:
                stateName = blockDirection == DefenseBlockDirection.Left
                    ? "LeftBlock"
                    : blockDirection == DefenseBlockDirection.Back ? "BackBlock" : "RightBlock";
                break;
            case P2pPresentationAction.Pass:
            case P2pPresentationAction.Shot:
                stateName = "Shoot";
                break;
            default:
                stateHash = 0;
                return false;
        }

        stateHash = Animator.StringToHash("Base Layer." + stateName);
        return true;
    }

    private AnimationClip FindClip(P2pPresentationAction action, DefenseBlockDirection blockDirection)
    {
        if (animator.runtimeAnimatorController == null || !TryGetStateHash(action, blockDirection, out _))
            return null;

        string clipName = action == P2pPresentationAction.Tackle ? "Slide" : action.ToString();
        if (action == P2pPresentationAction.Block)
            clipName = blockDirection == DefenseBlockDirection.Left ? "LeftBlock" : blockDirection == DefenseBlockDirection.Back ? "BackBlock" : "RightBlock";
        if (action == P2pPresentationAction.Pass || action == P2pPresentationAction.Shot)
            clipName = "Shoot";

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == clipName)
                return clip;
        }

        return null;
    }
}
