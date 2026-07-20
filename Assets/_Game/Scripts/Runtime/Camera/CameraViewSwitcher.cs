using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// F5로 카메라 시점을 전환한다.
/// 기본: 씬에 배치된 고정 시점(기존 화면 그대로).
/// 전환: 3인칭 - 플레이어 뒤 위쪽에서 뒤통수를 내려다보는 시점.
///
/// 카메라를 대상의 순간 방향(target.forward)에 그대로 붙이지 않고 yaw를 따로 감쇠시킨다.
/// 대상은 720°/s로 회전하는데, 카메라는 그 뒤 distance만큼 떨어진 원호를 돌기 때문에
/// 방향 전환/슬라이딩 종료처럼 방향이 급변하는 순간 회전이 그대로 증폭돼 화면이 휘둘린다.
/// </summary>
public class CameraViewSwitcher : MonoBehaviour
{
    [Header("Compatibility")]
    [SerializeField] private bool deferToActionCamera = true;

    [Tooltip("따라갈 대상. 비우면 이름이 'Player'인 오브젝트를 찾는다.")]
    [SerializeField] private Transform target;

    [Header("3인칭 시점")]
    [Tooltip("대상 뒤로 떨어지는 거리(m).")]
    [SerializeField] private float distance = 5f;
    [Tooltip("대상 위로 올라가는 높이(m).")]
    [SerializeField] private float height = 3f;
    [Tooltip("바라보는 지점을 대상 발밑에서 얼마나 올릴지(m). 머리 근처를 겨냥.")]
    [SerializeField] private float lookAtHeight = 1.2f;

    [Header("보간")]
    [Tooltip("위치 추종 속도. 클수록 즉각적으로 붙는다.")]
    [SerializeField] private float positionLerp = 10f;
    [Tooltip("회전 추종 속도.")]
    [SerializeField] private float rotationLerp = 10f;
    [Tooltip("카메라가 대상의 방향 전환을 따라잡는 데 걸리는 시간(초). 크게 잡을수록 급회전에도 화면이 덜 휘둘린다.")]
    [SerializeField] private float yawSmoothTime = 0.28f;

    private Vector3 defaultPosition;
    private Quaternion defaultRotation;
    private bool thirdPerson;
    private ThirdPersonActionCamera actionCamera;

    // 대상 yaw를 그대로 쓰지 않고 감쇠시킨 값. 카메라 배치의 기준이 된다.
    private float smoothedYaw;
    private float yawVelocity;

    /// <summary>지금 3인칭 시점인가. HUD 표시가 읽는다.</summary>
    public bool IsThirdPerson => thirdPerson;

    private void Awake()
    {
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;
        actionCamera = GetComponent<ThirdPersonActionCamera>();

        if (target == null)
        {
            GameObject go = GameObject.Find("Player");
            if (go != null) target = go.transform;
        }
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.f5Key.wasPressedThisFrame)
        {
            thirdPerson = !thirdPerson;
            if (thirdPerson) SnapToThirdPerson();
        }
    }

    private void LateUpdate()
    {
        if (deferToActionCamera && actionCamera != null && actionCamera.enabled)
            return;

        if (thirdPerson && target != null)
        {
            smoothedYaw = Mathf.SmoothDampAngle(smoothedYaw, target.eulerAngles.y, ref yawVelocity, yawSmoothTime);
            GetThirdPersonPose(smoothedYaw, out Vector3 pos, out Quaternion rot);
            transform.position = Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-positionLerp * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, defaultPosition, 1f - Mathf.Exp(-positionLerp * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, defaultRotation, 1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
        }
    }

    private void GetThirdPersonPose(float yaw, out Vector3 pos, out Quaternion rot)
    {
        Vector3 back = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        pos = target.position - back * distance + Vector3.up * height;
        Vector3 lookPoint = target.position + Vector3.up * lookAtHeight;
        rot = Quaternion.LookRotation(lookPoint - pos, Vector3.up);
    }

    /// <summary>전환 순간 부드럽게가 아니라 바로 붙여서 화면이 길게 흐르지 않게 한다.</summary>
    private void SnapToThirdPerson()
    {
        if (target == null) return;
        smoothedYaw = target.eulerAngles.y;
        yawVelocity = 0f;
        GetThirdPersonPose(smoothedYaw, out Vector3 pos, out Quaternion rot);
        transform.position = pos;
        transform.rotation = rot;
    }
}
