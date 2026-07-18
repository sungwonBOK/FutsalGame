using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사람 플레이어의 펀치(J) / 슬라이딩(K) 쿨다운을 화면 우하단에 원형 아이콘으로 표시한다.
/// 로직은 갖지 않고 CombatController의 공개 상태(PunchCooldown01 등)만 매 프레임 읽어 그린다.
/// (로직=CombatController, 표시=이 클래스 — MatchUI/ChargeGaugeUI와 동일한 분리 원칙.)
///
/// UI 계층을 씬에 손으로 배치하지 않고 코드로 생성하는 이유:
/// 프로젝트에 스프라이트 애셋이 없어 Image.fillAmount가 동작하지 않는다(ChargeGaugeUI의 주석 참고).
/// 원형 라디얼 쿨다운을 하려면 원판/링 스프라이트를 어차피 런타임에 만들어야 하므로,
/// 그 김에 계층 전체를 코드로 구성해 씬 YAML을 건드리지 않는다.
/// </summary>
public class AbilityCooldownUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("사람 플레이어의 CombatController. 이 값의 쿨다운을 표시한다.")]
    [SerializeField] private CombatController playerCombat;

    [Tooltip("사람 플레이어의 CharacterState. 기절 중 표시를 낮추는 데 쓴다. 비우면 playerCombat에서 자동으로 찾는다.")]
    [SerializeField] private CharacterState playerState;

    [Header("Layout")]
    [Tooltip("화면 우하단 모서리로부터의 여백(픽셀, 1920x1080 기준).")]
    [SerializeField] private Vector2 screenMargin = new Vector2(48f, 40f);

    [Tooltip("원형 아이콘 하나의 지름(픽셀).")]
    [SerializeField] private float pipSize = 88f;

    [Tooltip("두 아이콘 사이 간격(픽셀).")]
    [SerializeField] private float pipSpacing = 22f;

    [Header("Colors")]
    [Tooltip("펀치 아이콘 기본색.")]
    [SerializeField] private Color punchColor = new Color(0.98f, 0.62f, 0.16f);

    [Tooltip("슬라이딩 아이콘 기본색.")]
    [SerializeField] private Color slideColor = new Color(0.30f, 0.68f, 0.98f);

    [Tooltip("쿨다운 중 아이콘이 어두워지는 정도(0=완전히 검게, 1=그대로).")]
    [Range(0f, 1f)]
    [SerializeField] private float cooldownDim = 0.42f;

    [Tooltip("쿨다운 남은 부분을 덮는 어둠의 색.")]
    [SerializeField] private Color sweepColor = new Color(0.02f, 0.03f, 0.06f, 0.78f);

    [Tooltip("쿨다운 중에 키를 눌렀을 때(사용 불가) 번지는 경고색.")]
    [SerializeField] private Color rejectColor = new Color(0.95f, 0.22f, 0.22f);

    [Header("Feedback (연출)")]
    [Tooltip("쿨다운이 끝나 준비 완료된 순간의 번쩍임/팝 지속 시간(초).")]
    [SerializeField] private float readyFlashDuration = 0.25f;

    [Tooltip("준비 완료 순간 아이콘이 커지는 최대 배율.")]
    [SerializeField] private float readyPopScale = 1.18f;

    [Tooltip("쿨다운 중 키를 눌렀을 때 흔들리는 시간(초).")]
    [SerializeField] private float rejectShakeDuration = 0.2f;

    [Tooltip("거절 흔들림의 좌우 진폭(픽셀).")]
    [SerializeField] private float rejectShakeAmount = 5f;

    // 런타임 생성 스프라이트(정적 캐시 — 씬당 1회만 만든다).
    private static Sprite discSprite;
    private static Sprite ringSprite;

    private RectTransform root;
    private Pip punchPip;
    private Pip slidePip;

    /// <summary>아이콘 하나(원판+링+스윕+라벨)와 그 연출 타이머를 묶은 단위. 펀치/슬라이딩이 같은 코드 경로를 공유한다.</summary>
    private class Pip
    {
        public RectTransform root;   // 위치(흔들림) 담당
        public RectTransform disc;   // 스케일(팝) 담당
        public Image baseImg;
        public Image sweepImg;
        public Image ringImg;
        public Text keyLabel;
        public Text timeLabel;
        public Color accent;

        public Vector2 homePos;
        public bool wasCooling;
        public float readyFlashStart = -999f;
        public float shakeStart = -999f;
        public float lastSeenReject = -999f;
    }

    private void Awake()
    {
        if (playerState == null && playerCombat != null)
            playerState = playerCombat.GetComponent<CharacterState>();

        BuildHierarchy();
    }

    private void Update()
    {
        if (root == null) return;

        // 경기 중이 아니거나(카운트다운/일시정지/종료) 참조가 없으면 숨긴다.
        bool visible = playerCombat != null && GameManager.PlayActive;
        if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        if (!visible) return;

        bool stunned = playerState != null && playerState.IsStunned;

        UpdatePip(punchPip, playerCombat.PunchCooldown01, playerCombat.PunchRemaining,
                  playerCombat.LastPunchRejectedTime, false, stunned);

        UpdatePip(slidePip, playerCombat.SlideCooldown01, playerCombat.SlideRemaining,
                  playerCombat.LastSlideRejectedTime, playerCombat.IsSliding, stunned);
    }

    /// <summary>아이콘 하나를 현재 상태에 맞춰 갱신한다. 상태를 만들지 않고 읽은 값만 그린다.</summary>
    private void UpdatePip(Pip p, float cool01, float remaining, float rejectTime, bool isActive, bool stunned)
    {
        bool cooling = cool01 > 0f;

        // 쿨다운이 끝난 프레임을 잡아 준비 완료 연출을 시작한다.
        if (p.wasCooling && !cooling) p.readyFlashStart = Time.unscaledTime;
        p.wasCooling = cooling;

        // 쿨다운 중 입력이 거절됐으면 흔들림을 시작한다.
        if (rejectTime > p.lastSeenReject)
        {
            p.lastSeenReject = rejectTime;
            p.shakeStart = Time.unscaledTime;
        }

        // 남은 만큼 어둠으로 덮는다. 12시에서 시계방향으로 밝은 영역이 드러난다.
        p.sweepImg.fillAmount = cool01;

        // 원판 색: 쿨다운 중엔 어둡게, 준비되면 선명하게. 슬라이딩 발동 중엔 밝게 강조.
        Color accent = p.accent;
        Color baseCol = cooling ? Dim(accent, cooldownDim) : accent;
        if (isActive) baseCol = Color.Lerp(accent, Color.white, 0.45f);

        // 거절 흔들림 동안 붉게 물들인다.
        float shakeT = Time.unscaledTime - p.shakeStart;
        bool shaking = shakeT >= 0f && shakeT < rejectShakeDuration;
        if (shaking)
        {
            float k = 1f - (shakeT / rejectShakeDuration);
            baseCol = Color.Lerp(baseCol, rejectColor, k);
        }

        if (stunned) baseCol = Desaturate(Dim(baseCol, 0.5f));
        p.baseImg.color = baseCol;

        // 준비 완료 연출: 링이 흰색으로 번쩍이고 아이콘이 살짝 커졌다 돌아온다.
        float flashT = Time.unscaledTime - p.readyFlashStart;
        bool flashing = flashT >= 0f && flashT < readyFlashDuration;
        float flash01 = flashing ? 1f - (flashT / readyFlashDuration) : 0f;

        Color ringCol = cooling ? Dim(accent, 0.55f) : Color.Lerp(accent, Color.white, 0.35f);
        if (isActive) ringCol = Color.white;
        if (flashing) ringCol = Color.Lerp(ringCol, Color.white, flash01);
        if (stunned) ringCol = Desaturate(Dim(ringCol, 0.5f));
        p.ringImg.color = ringCol;

        // 0 -> 1 -> 0 곡선(sin)으로 부드럽게 팝. 코루틴 없이 타이머만으로 처리한다.
        float pop = flashing ? Mathf.Sin(flash01 * Mathf.PI) * (readyPopScale - 1f) : 0f;
        p.disc.localScale = Vector3.one * (1f + pop);

        // 흔들림은 좌우로만, 빠르게 감쇠시킨다.
        float shakeX = shaking
            ? Mathf.Sin(shakeT * 70f) * rejectShakeAmount * (1f - shakeT / rejectShakeDuration)
            : 0f;
        p.root.anchoredPosition = p.homePos + new Vector2(shakeX, 0f);

        // 쿨다운 중엔 남은 초, 준비되면 키 글자.
        bool showTime = cooling && remaining >= 0.05f;
        if (p.timeLabel.gameObject.activeSelf != showTime) p.timeLabel.gameObject.SetActive(showTime);
        if (p.keyLabel.gameObject.activeSelf == showTime) p.keyLabel.gameObject.SetActive(!showTime);
        if (showTime) p.timeLabel.text = remaining.ToString("0.0");
    }

    /// <summary>알파는 유지한 채 밝기만 낮춘다.</summary>
    private static Color Dim(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

    /// <summary>휘도 기준 회색조로 바꾼다(기절 중 "지금은 못 쓴다" 표현).</summary>
    private static Color Desaturate(Color c)
    {
        float g = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        return new Color(g, g, g, c.a);
    }

    // ---------------- 계층 생성 ----------------

    private void BuildHierarchy()
    {
        root = NewRect("AbilityCooldownRoot", (RectTransform)transform);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 0f);
        root.sizeDelta = new Vector2(pipSize * 2f + pipSpacing, pipSize + 26f);
        root.anchoredPosition = new Vector2(-screenMargin.x, screenMargin.y);

        // 오른쪽이 슬라이딩(K), 그 왼쪽이 펀치(J).
        punchPip = BuildPip("Pip_Punch", "J", "펀치", punchColor, new Vector2(0f, 0f));
        slidePip = BuildPip("Pip_Slide", "K", "슬라이딩", slideColor, new Vector2(pipSize + pipSpacing, 0f));
    }

    private Pip BuildPip(string name, string key, string label, Color accent, Vector2 pos)
    {
        Pip p = new Pip { accent = accent, homePos = pos, wasCooling = false };

        p.root = NewRect(name, root);
        p.root.anchorMin = p.root.anchorMax = p.root.pivot = new Vector2(0f, 0f);
        p.root.sizeDelta = new Vector2(pipSize, pipSize + 26f);
        p.root.anchoredPosition = pos;

        // 원형 부분. 팝 스케일이 이름 라벨까지 흔들지 않도록 별도 컨테이너로 감싼다.
        p.disc = NewRect("Disc", p.root);
        p.disc.anchorMin = p.disc.anchorMax = p.disc.pivot = new Vector2(0.5f, 1f);
        p.disc.sizeDelta = new Vector2(pipSize, pipSize);
        p.disc.anchoredPosition = Vector2.zero;

        // 아래에서 위로 겹치는 순서: 원판 → 쿨다운 어둠 → 링(테두리) → 라벨.
        p.baseImg = NewImage("Base", p.disc, GetDisc(), accent);
        p.sweepImg = NewImage("Sweep", p.disc, GetDisc(), sweepColor);
        p.sweepImg.type = Image.Type.Filled;
        p.sweepImg.fillMethod = Image.FillMethod.Radial360;
        p.sweepImg.fillOrigin = (int)Image.Origin360.Top;
        // 반시계 채움 → 남은 어둠이 12시를 기준으로 줄어들며 밝은 영역이 시계방향으로 드러난다.
        p.sweepImg.fillClockwise = false;
        p.sweepImg.fillAmount = 0f;

        p.ringImg = NewImage("Ring", p.disc, GetRing(), Color.white);

        p.keyLabel = NewText("KeyLabel", p.disc, key, 34, TextAnchor.MiddleCenter);
        p.keyLabel.fontStyle = FontStyle.Bold;
        StretchFull(p.keyLabel.rectTransform);

        p.timeLabel = NewText("TimeLabel", p.disc, "", 30, TextAnchor.MiddleCenter);
        p.timeLabel.fontStyle = FontStyle.Bold;
        StretchFull(p.timeLabel.rectTransform);
        p.timeLabel.gameObject.SetActive(false);

        Text nameLabel = NewText("NameLabel", p.root, label, 18, TextAnchor.LowerCenter);
        RectTransform nr = nameLabel.rectTransform;
        nr.anchorMin = new Vector2(0f, 0f);
        nr.anchorMax = new Vector2(1f, 0f);
        nr.pivot = new Vector2(0.5f, 0f);
        nr.sizeDelta = new Vector2(0f, 24f);
        nr.anchoredPosition = Vector2.zero;
        nameLabel.color = new Color(1f, 1f, 1f, 0.75f);

        return p;
    }

    private static RectTransform NewRect(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static Image NewImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        StretchFull(rt);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false; // HUD는 클릭을 가로채면 안 된다.
        return img;
    }

    private static Text NewText(string name, RectTransform parent, string content, int size, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        ((RectTransform)go.transform).SetParent(parent, false);

        Text t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = content;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ---------------- 프로시저럴 스프라이트 ----------------

    private static Sprite GetDisc()
    {
        if (discSprite == null) discSprite = BuildCircle(256, 0f);
        return discSprite;
    }

    private static Sprite GetRing()
    {
        if (ringSprite == null) ringSprite = BuildCircle(256, 0.88f);
        return ringSprite;
    }

    /// <summary>
    /// 흰색 원(innerRatio=0) 또는 링(innerRatio>0) 스프라이트를 만든다.
    /// 프로젝트에 스프라이트 애셋이 없어 Image.fillAmount를 쓸 수 없으므로 런타임에 생성한다.
    /// 가장자리 알파를 1.5px 폭으로 보간해 계단 현상을 없앤다.
    /// </summary>
    private static Sprite BuildCircle(int size, float innerRatio)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        float center = (size - 1) * 0.5f;
        float outer = size * 0.5f - 1f;
        float inner = outer * innerRatio;
        const float edge = 1.5f; // 안티에일리어싱 폭(픽셀)

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float a = Mathf.Clamp01((outer - d) / edge);
                if (innerRatio > 0f) a = Mathf.Min(a, Mathf.Clamp01((d - inner) / edge));

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
}
