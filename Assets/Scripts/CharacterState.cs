using UnityEngine;

/// <summary>
/// 캐릭터 공용 상태(정상/기절). 플레이어와 상대(더미/AI)가 동일하게 사용한다.
/// 피격 시 넉백 임펄스를 주고 일정 시간 기절시킨다. 기절 중에는 다른 컴포넌트들이
/// 이 상태(IsStunned)를 확인해 이동·전투·공 소유를 모두 막는다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CharacterState : MonoBehaviour
{
    private Rigidbody rb;
    private float stunUntil = -999f;

    /// <summary>현재 기절 중인가.</summary>
    public bool IsStunned { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // 기절 시간이 끝나면 자동으로 정상 복귀.
        if (IsStunned && Time.time >= stunUntil)
            IsStunned = false;
    }

    /// <summary>
    /// 피격 처리: 뒤로 밀리는 넉백 임펄스를 주고 stunDuration 동안 기절시킨다.
    /// 이미 기절 중이면 더 긴 쪽으로 연장한다.
    /// </summary>
    public void ApplyHit(Vector3 knockbackImpulse, float stunDuration)
    {
        IsStunned = true;
        stunUntil = Mathf.Max(stunUntil, Time.time + stunDuration);
        rb.AddForce(knockbackImpulse, ForceMode.Impulse);
    }

    /// <summary>기절 등 상태를 초기화한다(킥오프 리셋용).</summary>
    public void ResetState()
    {
        IsStunned = false;
        stunUntil = -999f;
    }

}
