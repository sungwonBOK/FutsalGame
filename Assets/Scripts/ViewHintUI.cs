using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 좌하단에 "F5: 시점 전환"과 현재 시점을 표시한다.
/// 로직은 갖지 않고 CameraViewSwitcher의 공개 상태(IsThirdPerson)만 읽어 그린다.
/// (AbilityCooldownUI와 같은 이유로 계층을 코드로 만든다 — 씬 YAML을 건드리지 않는다.)
/// </summary>
public class ViewHintUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("표시할 대상 시점 전환기. 비우면 메인 카메라에서 찾는다.")]
    [SerializeField] private CameraViewSwitcher switcher;

    [Header("Layout")]
    [Tooltip("화면 좌하단 모서리로부터의 여백(픽셀, 1920x1080 기준).")]
    [SerializeField] private Vector2 screenMargin = new Vector2(48f, 40f);
    [Tooltip("글자 크기(픽셀).")]
    [SerializeField] private int fontSize = 20;

    private Text label;

    private void Awake()
    {
        if (switcher == null && Camera.main != null)
            switcher = Camera.main.GetComponent<CameraViewSwitcher>();

        Build();
    }

    private void Update()
    {
        if (label == null) return;

        bool visible = switcher != null;
        if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
        if (!visible) return;

        label.text = "F5: 시점 전환  —  현재: " + (switcher.IsThirdPerson ? "3인칭" : "기본");
    }

    private void Build()
    {
        GameObject go = new GameObject("ViewHint", typeof(RectTransform), typeof(Text));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent((RectTransform)transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(420f, 28f);
        rt.anchoredPosition = screenMargin;

        label = go.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.alignment = TextAnchor.LowerLeft;
        label.color = new Color(1f, 1f, 1f, 0.75f);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
    }
}
