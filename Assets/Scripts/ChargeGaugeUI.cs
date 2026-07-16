using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사람 플레이어의 슛 차징 정도를 보여주는 파워 게이지 바.
/// 스크린 스페이스(Overlay) UI 요소를 플레이어 월드 위치(머리 위)에 투영해 캐릭터를 따라다니게 한다.
/// (별도 World-Space Canvas/빌보드 없이 항상 카메라를 향하고 선명하다.)
/// 로직은 갖지 않고 PlayerBallHandler의 IsCharging / ChargeAmount01만 읽어 표시한다.
/// </summary>
public class ChargeGaugeUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("사람 플레이어의 PlayerBallHandler. 이 값의 차징 상태를 표시한다.")]
    [SerializeField] private PlayerBallHandler playerHandler;

    [Tooltip("따라 움직일 게이지 컨테이너(RectTransform). 평소 비활성, 차징 중에만 표시된다.")]
    [SerializeField] private RectTransform gaugeRoot;

    [Tooltip("채움 이미지(Image Type=Filled, Horizontal). fillAmount로 차징 정도를 표현한다.")]
    [SerializeField] private Image fillImage;

    [Tooltip("게이지를 띄울 월드 대상(보통 플레이어 Transform).")]
    [SerializeField] private Transform worldTarget;

    [Tooltip("대상 위치에서 위로 얼마나 띄울지(머리 위 오프셋, 월드 단위).")]
    [SerializeField] private float headHeight = 2.2f;

    [Tooltip("월드→스크린 투영에 쓸 카메라. 비우면 Camera.main 사용.")]
    [SerializeField] private Camera cam;

    [Header("Fill Color")]
    [Tooltip("차징 0(약한 패스)일 때 색.")]
    [SerializeField] private Color lowColor = new Color(0.3f, 0.9f, 0.3f);   // green
    [Tooltip("차징 1(풀차지 슛)일 때 색.")]
    [SerializeField] private Color highColor = new Color(0.95f, 0.25f, 0.2f); // red

    private RectTransform parentRect;

    private void Awake()
    {
        if (gaugeRoot != null) parentRect = gaugeRoot.parent as RectTransform;
    }

    private void LateUpdate()
    {
        if (playerHandler == null || gaugeRoot == null) return;

        // 차징 중이 아니면 숨긴다.
        if (!playerHandler.IsCharging)
        {
            if (gaugeRoot.gameObject.activeSelf) gaugeRoot.gameObject.SetActive(false);
            return;
        }

        if (cam == null) cam = Camera.main;
        if (cam == null || worldTarget == null) return;

        // 대상 머리 위 월드 지점을 스크린으로 투영.
        Vector3 world = worldTarget.position + Vector3.up * headHeight;
        Vector3 screen = cam.WorldToScreenPoint(world);

        // 카메라 뒤면 숨긴다.
        if (screen.z < 0f)
        {
            if (gaugeRoot.gameObject.activeSelf) gaugeRoot.gameObject.SetActive(false);
            return;
        }

        if (!gaugeRoot.gameObject.activeSelf) gaugeRoot.gameObject.SetActive(true);

        // 스크린 좌표 → 부모 RectTransform 로컬 좌표(Overlay 캔버스는 카메라 인자 null).
        if (parentRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, null, out Vector2 local))
        {
            gaugeRoot.anchoredPosition = local;
        }

        // 채움/색 갱신.
        float c = playerHandler.ChargeAmount01;
        if (fillImage != null)
        {
            fillImage.fillAmount = c;
            fillImage.color = Color.Lerp(lowColor, highColor, c);
        }
    }
}
