using UnityEngine;

/// <summary>
/// 골대 안쪽의 트리거 영역. 공이 들어오면 GameManager에 득점을 통보한다.
/// 어느 골이 누구 득점인지는 playerScoresHere로 매핑한다.
///  - 공격하던 쪽이 득점하는 구조이므로, "플레이어가 공격하는 골"에는 playerScoresHere=true,
///    "AI가 공격하는 골"에는 false를 설정한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    [Tooltip("이 골에 공이 들어오면 플레이어 득점이면 true, 상대(AI) 득점이면 false.")]
    [SerializeField] private bool playerScoresHere;

    [Tooltip("공 오브젝트 이름 (이 이름의 Rigidbody만 득점으로 인정).")]
    [SerializeField] private string ballObjectName = "Ball";

    private void Reset()
    {
        // 컴포넌트 추가 시 자동으로 트리거로 설정.
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 온라인에서는 공이 클라이언트에서도 트리거를 통과하므로, 득점 판정은 서버만 한다.
        // (클라가 각자 세면 같은 골이 여러 번 들어간다.)
        if (!NetworkBall.LocalHasAuthority) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;
        if (rb.gameObject.name != ballObjectName) return;

        if (GameManager.Instance != null)
            GameManager.Instance.GoalScored(playerScoresHere, transform.position);
    }
}
