using UnityEngine;

/// <summary>
/// 캐릭터 공용 이동/회전. 키 입력을 직접 읽지 않고 SetMoveInput()으로 이동 방향을 주입받는다.
/// (사람은 PlayerInput이, 이후 상대는 AI가 이 컴포넌트를 동일하게 구동한다.)
/// 기절 중에는 이동/회전을 멈춰 넉백 물리가 그대로 적용되게 한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CharacterState))]
public class CharacterMotor : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("이동 속도 (units/sec).")]
    [SerializeField] private float moveSpeed = 6f;

    [Tooltip("회전 속도 (deg/sec).")]
    [SerializeField] private float turnSpeed = 720f;

    private Rigidbody rb;
    private CharacterState state;
    private Vector3 moveInput;

    // 슬라이딩 대시 상태 (CombatController가 요청).
    private Vector3 dashVelocity;
    private float dashUntil = -999f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        state = GetComponent<CharacterState>();
        // X/Z 회전을 잠가 넘어지지 않게 한다. Y 회전(방향 전환)은 열어둔다.
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        // 물리는 50Hz로 도는데 카메라는 매 프레임 이 몸을 따라간다. 보간이 없으면 슬라이딩 대시처럼
        // 빠른 이동에서 한 스텝에 0.24m씩 튀는 게 그대로 화면 떨림으로 보인다.
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    /// <summary>이동 방향 주입 (XZ 평면). 크기가 1을 넘으면 정규화한다.</summary>
    public void SetMoveInput(Vector3 dir)
    {
        dir.y = 0f;
        moveInput = dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }

    /// <summary>일정 시간 동안 지정 속도로 대시(슬라이딩). 그동안 이동 입력은 무시한다.</summary>
    public void Dash(Vector3 velocity, float duration)
    {
        dashVelocity = velocity;
        dashUntil = Time.time + duration;
    }

    private void FixedUpdate()
    {
        // 기절 중엔 이동/회전을 하지 않는다 → 넉백 임펄스가 그대로 살아있게.
        if (state.IsStunned)
            return;

        Vector3 current = rb.linearVelocity;

        // 슬라이딩 대시 중: 방향/속도를 고정하고 이동 입력을 무시.
        if (Time.time < dashUntil)
        {
            rb.linearVelocity = new Vector3(dashVelocity.x, current.y, dashVelocity.z);
            return;
        }

        // 일반 이동: Y축 속도(중력)는 유지, XZ만 제어.
        Vector3 target = moveInput * moveSpeed;
        rb.linearVelocity = new Vector3(target.x, current.y, target.z);

        // 이동 방향을 바라보게 회전. 입력이 없으면 마지막 방향 유지.
        if (moveInput.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveInput, Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));
        }
    }
}
