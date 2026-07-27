using UnityEngine;

/// <summary>
/// 캐릭터 공용 상태(정상/기절). 플레이어와 상대(더미/AI)가 동일하게 사용한다.
/// 피격 시 넉백 임펄스를 주고 일정 시간 기절시킨다. 기절 중에는 다른 컴포넌트들이
/// 이 상태(IsStunned)를 확인해 이동·전투·공 소유를 모두 막는다.
///
/// 온라인 경기에서는 역할이 갈린다.
///  - 기절 여부는 서버가 정하고 모두에게 복제된다(클라가 제멋대로 풀면 안 되므로).
///  - 넉백은 이동이라 소유자만 적용할 수 있어, 서버가 소유자에게 밀어달라고 부탁한다.
///  - 회피 무적은 소유자가 시작하므로, 소유자가 서버에 알려줘야 서버 판정에서도 인정된다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CharacterState : MonoBehaviour
{
    private Rigidbody rb;
    private NetworkPlayerAgent netAgent;
    private float stunUntil = -999f;
    private float invulnerableUntil = -999f;

    /// <summary>현재 기절 중인가.</summary>
    public bool IsStunned { get; private set; }
    public bool IsInvulnerable => Time.time < invulnerableUntil;
    public float LastEvadeTime { get; private set; } = -999f;

    /// <summary>기절 시간을 직접 관리해도 되는지. 오프라인이면 항상, 온라인이면 서버만.</summary>
    private bool HasStateAuthority => netAgent == null || !netAgent.IsSpawned || netAgent.IsServer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        netAgent = GetComponent<NetworkPlayerAgent>();
    }

    private void Update()
    {
        // 기절이 풀리는 시점도 서버가 정한다. 클라는 복제받은 값을 따른다.
        if (!HasStateAuthority)
            return;

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

        // 온라인에서는 이동 권한이 소유자에게 있어 서버가 직접 밀어도 곧 덮어써진다.
        if (netAgent != null && netAgent.IsSpawned)
            netAgent.ServerApplyKnockback(knockbackImpulse);
        else
            rb.AddForce(knockbackImpulse, ForceMode.Impulse);
    }

    public void SetInvulnerable(float duration)
    {
        ApplyInvulnerability(duration);

        // 회피는 내 클라이언트에서 시작되므로, 서버가 모르면 무적이 무시된다.
        if (netAgent != null && netAgent.IsSpawned && netAgent.IsOwner && !netAgent.IsServer)
            netAgent.ReportInvulnerabilityRpc(duration);
    }

    /// <summary>네트워크로 전달받은 무적을 적용한다(다시 알리지 않는다).</summary>
    public void ApplyInvulnerability(float duration)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + Mathf.Max(0f, duration));
    }

    /// <summary>서버가 복제한 기절 상태를 그대로 반영한다.</summary>
    public void MirrorStun(bool stunned)
    {
        IsStunned = stunned;
    }

    /// <summary>서버가 임펄스를 지시했을 때 소유자가 실제로 밀리는 지점.</summary>
    public void ApplyKnockbackImpulse(Vector3 impulse)
    {
        if (rb != null && !rb.isKinematic)
            rb.AddForce(impulse, ForceMode.Impulse);
    }

    public void NotifyEvaded()
    {
        LastEvadeTime = Time.time;
    }

    /// <summary>기절 등 상태를 초기화한다(킥오프 리셋용).</summary>
    public void ResetState()
    {
        IsStunned = false;
        stunUntil = -999f;
        invulnerableUntil = -999f;
    }
}
