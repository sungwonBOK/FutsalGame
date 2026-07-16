using UnityEngine;

/// <summary>
/// 공에 부착. 공이 일정 속도 이상으로 벽에 부딪히면 충돌 지점에 이펙트/소리를 1회 재생한다.
/// 게임 물리(반발/속도)는 건드리지 않고 연출만 얹는다.
/// </summary>
public class BallImpactEffect : MonoBehaviour
{
    [Tooltip("벽 충돌 이펙트 프리팹.")]
    [SerializeField] private GameObject wallImpactPrefab;

    [Tooltip("이 상대속도 이상일 때만 이펙트/소리 재생(너무 자주 나지 않게).")]
    [SerializeField] private float minSpeed = 6f;

    [Tooltip("이 이름으로 시작하는 오브젝트와의 충돌만 벽으로 인정.")]
    [SerializeField] private string wallNamePrefix = "Wall";

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude < minSpeed) return;
        if (!collision.gameObject.name.StartsWith(wallNamePrefix)) return;

        ContactPoint contact = collision.GetContact(0);
        if (wallImpactPrefab != null)
            Instantiate(wallImpactPrefab, contact.point, Quaternion.LookRotation(contact.normal));

        // 연출: 벽 튕김 소리 (매니저가 속도/간격 가드로 과다 재생 방지)
        if (AudioManager.Instance != null) AudioManager.Instance.PlayWallBounce();
    }
}
