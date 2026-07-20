using UnityEngine;

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
        if (owner == null)
            return false;
        if (CurrentOwner != null && CurrentOwner != owner)
            return false;

        CurrentOwner = owner;
        SetPossessionPhysics();
        return true;
    }

    public bool HasOwner(PlayerBallHandler owner)
    {
        return owner != null && CurrentOwner == owner;
    }

    public bool Release(PlayerBallHandler owner, Vector3 impulse)
    {
        if (!HasOwner(owner))
            return false;

        ReleaseCurrentOwner(impulse);
        return true;
    }

    public void ClearOwner()
    {
        if (CurrentOwner == null)
            return;

        CurrentOwner = null;
        RestoreFreeBallPhysics(Vector3.zero);
    }

    public static void ClearActiveOwner()
    {
        if (ActiveBall != null)
            ActiveBall.ClearOwner();
    }

    public void MoveToDribblePosition(PlayerBallHandler owner, Vector3 position)
    {
        if (!HasOwner(owner))
            return;

        transform.position = position;
    }

    private void ReleaseCurrentOwner(Vector3 impulse)
    {
        CurrentOwner = null;
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
