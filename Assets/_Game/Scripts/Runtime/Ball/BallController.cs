using UnityEngine;

/// <summary>
/// 공의 물리와 소유 상태를 관리한다.
///
/// 온라인 경기에서는 공에 관한 판단이 전부 서버에서만 이뤄져야 하므로,
/// 상태를 바꾸는 모든 진입점에서 권한(<see cref="NetworkBall.LocalHasAuthority"/>)을 확인한다.
/// 권한이 없는 쪽에서는 조용히 무시되고, 대신 서버가 복제해준 소유 상태를 받아 반영한다.
/// 오프라인 플레이에서는 항상 권한이 있으므로 기존과 똑같이 동작한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BallController : MonoBehaviour
{
    public static BallController ActiveBall { get; private set; }

    [SerializeField] private BallConfig config;

    private Rigidbody body;
    private Collider ballCollider;
    private BallConfig runtimeConfig;

    public PlayerBallHandler CurrentOwner { get; private set; }
    public Rigidbody Body => body;
    public float OwnerMaxDistance => Config.Possession.ownerMaxDistance;

    private BallConfig Config
    {
        get
        {
            if (config == null)
            {
                if (runtimeConfig == null)
                    runtimeConfig = ScriptableObject.CreateInstance<BallConfig>();
                return runtimeConfig;
            }

            return config;
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        ballCollider = GetComponent<Collider>();
        if (ActiveBall == null || ActiveBall == this)
            ActiveBall = this;
    }

    private void OnEnable()
    {
        if (ActiveBall == null || ActiveBall == this)
            ActiveBall = this;
    }

    private void Update()
    {
        // 소유자와 너무 멀어지면 공을 놓는 판정. 서버만 내린다.
        if (!NetworkBall.LocalHasAuthority)
            return;

        float maxOwnerDistance = OwnerMaxDistance;
        if (CurrentOwner == null || maxOwnerDistance <= 0f)
            return;

        Vector3 ownerPosition = CurrentOwner.transform.position;
        Vector3 ballPosition = transform.position;
        ownerPosition.y = 0f;
        ballPosition.y = 0f;

        if ((ownerPosition - ballPosition).sqrMagnitude > maxOwnerDistance * maxOwnerDistance)
            ClearOwner();
    }

    public bool TryAcquire(PlayerBallHandler owner)
    {
        bool p2pAcquireResult;
        if (BallAuthorityController.TryHandleAcquire(this, owner, out p2pAcquireResult))
            return p2pAcquireResult;

        return TryAcquireWithLocalAuthority(owner);
    }

    /// <summary>Used only by the current BallAuthority after it has validated a P2P acquire request.</summary>
    public bool TryAcquireFromP2pAuthority(PlayerBallHandler owner)
    {
        if (BallAuthorityController.Current == null || !BallAuthorityController.Current.IsLocalAuthority)
            return false;

        return TryAcquireWithLocalAuthority(owner);
    }

    private bool TryAcquireWithLocalAuthority(PlayerBallHandler owner)
    {
        if (!NetworkBall.LocalHasAuthority)
            return false;
        if (owner == null)
            return false;
        if (CurrentOwner != null && CurrentOwner != owner)
            return false;

        SetOwner(owner);
        SetPossessionPhysics();
        return true;
    }

    /// <summary>
    /// 서버가 복제해준 소유자를 그대로 반영한다(위치는 NetworkTransform이 가져온다).
    /// 클라이언트에서 HasBall 같은 판단이 맞으려면 이 값이 서버와 같아야 한다.
    ///
    /// 콜라이더도 함께 맞춘다. 소유 중에는 공이 선수 발밑에 붙어 있어서,
    /// 클라이언트에만 콜라이더가 살아 있으면 그 선수를 밀어 떨리게 만든다.
    /// </summary>
    public void MirrorOwner(PlayerBallHandler owner)
    {
        CurrentOwner = owner;

        if (ballCollider != null)
            ballCollider.enabled = owner == null;
    }

    /// <summary>소유자를 바꾸고, 온라인이면 모든 클라에 알린다.</summary>
    private void SetOwner(PlayerBallHandler owner)
    {
        CurrentOwner = owner;

        if (NetworkBall.Instance != null)
            NetworkBall.Instance.ServerPublishOwner(owner);
    }

    public bool HasOwner(PlayerBallHandler owner)
    {
        return owner != null && CurrentOwner == owner;
    }

    public bool Release(PlayerBallHandler owner, Vector3 impulse)
    {
        if (!NetworkBall.LocalHasAuthority)
            return false;
        if (!HasOwner(owner))
            return false;

        ReleaseCurrentOwner(impulse);
        return true;
    }

    public void ClearOwner()
    {
        if (!NetworkBall.LocalHasAuthority)
            return;
        if (CurrentOwner == null)
            return;

        SetOwner(null);
        RestoreFreeBallPhysics(Vector3.zero);
    }

    /// <summary>Reverts a just-applied P2P acquisition if its reliable authority transfer cannot be sent.</summary>
    public void ClearOwnerFromP2pAuthority()
    {
        if (BallAuthorityController.Current == null || !BallAuthorityController.Current.IsLocalAuthority)
            return;
        if (CurrentOwner == null)
            return;

        SetOwner(null);
        RestoreFreeBallPhysics(Vector3.zero);
    }

    /// <summary>
    /// Applies a direct-P2P authority anchor or latest state without writing NGO NetworkVariables.
    /// Non-authorities keep the rigidbody kinematic so only the BallAuthority resolves physics.
    /// </summary>
    public void ApplyP2pState(
        PlayerBallHandler owner,
        Vector3 position,
        Quaternion rotation,
        Vector3 velocity,
        Vector3 angularVelocity,
        bool localAuthority)
    {
        CurrentOwner = owner;
        transform.SetPositionAndRotation(position, rotation);

        if (ballCollider != null)
            ballCollider.enabled = owner == null;

        if (body == null)
            return;

        body.position = position;
        body.rotation = rotation;
        body.isKinematic = !localAuthority || owner != null;
        body.linearVelocity = owner == null && localAuthority ? velocity : Vector3.zero;
        body.angularVelocity = owner == null && localAuthority ? angularVelocity : Vector3.zero;
    }

    public static void ClearActiveOwner()
    {
        if (ActiveBall != null)
            ActiveBall.ClearOwner();
    }

    public void MoveToDribblePosition(PlayerBallHandler owner, Vector3 position)
    {
        // 드리블 중 공 위치도 서버가 정하고 클라는 복제받는다.
        if (!NetworkBall.LocalHasAuthority)
            return;
        if (!HasOwner(owner))
            return;

        Vector3 current = transform.position;
        Vector3 next = Vector3.Lerp(current, position, 1f - Mathf.Exp(-Config.DribbleFollowSharpness * Time.deltaTime));
        Vector3 lag = next - position;
        if (lag.sqrMagnitude > Config.DribbleMaxFollowLag * Config.DribbleMaxFollowLag)
            next = position + lag.normalized * Config.DribbleMaxFollowLag;

        RotateForDribbleMotion(next - current);
        transform.position = next;
    }

    public void AddReleaseVelocity(Vector3 velocity)
    {
        if (!NetworkBall.LocalHasAuthority)
            return;

        if (CurrentOwner == null && body != null)
            body.linearVelocity += velocity;
    }

    public void AddReleaseImpulse(Vector3 impulse)
    {
        if (!NetworkBall.LocalHasAuthority)
            return;

        if (CurrentOwner == null && body != null && impulse.sqrMagnitude > 0.0001f)
            body.AddForce(impulse, ForceMode.Impulse);
    }

    private void RotateForDribbleMotion(Vector3 delta)
    {
        delta.y = 0f;
        if (delta.sqrMagnitude < 1e-8f || ballCollider == null) return;

        float radius = Mathf.Max(0.0001f, ballCollider.bounds.extents.x);
        Vector3 axis = Vector3.Cross(Vector3.up, delta.normalized);
        transform.rotation = Quaternion.AngleAxis(delta.magnitude / radius * Mathf.Rad2Deg, axis) * transform.rotation;
    }

    private void ReleaseCurrentOwner(Vector3 impulse)
    {
        SetOwner(null);
        RestoreFreeBallPhysics(impulse);
    }

    private void SetPossessionPhysics()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        if (ballCollider != null)
            ballCollider.enabled = false;
    }

    private void RestoreFreeBallPhysics(Vector3 impulse)
    {
        if (ballCollider != null)
            ballCollider.enabled = true;

        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        if (impulse.sqrMagnitude > 0.0001f)
            body.AddForce(impulse, ForceMode.Impulse);
    }

    private void OnDisable()
    {
        if (ActiveBall == this)
            ActiveBall = null;
    }

    private void OnDestroy()
    {
        if (runtimeConfig != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeConfig);
            else
                DestroyImmediate(runtimeConfig);
        }
    }
}
