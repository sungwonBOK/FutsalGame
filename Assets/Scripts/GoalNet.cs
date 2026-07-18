using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 절차적 Verlet 천 네트. 웨지형(앞상단 크로스바 → 뒤바닥 바) 격자 메시를 만들고,
/// 가장자리(프레임 부착부)는 핀 고정, 내부는 자유롭게 흔들리며, 공이 근처에 오면
/// 정점을 공 표면 밖으로 밀어내 "골 들어갈 때 그물이 불룩/펄럭"인다.
/// 게임 물리와 무관(콜라이더 없음) — 순수 연출. 공은 이름 "Ball"로 자동 검색.
/// ExecuteAlways로 에디트 모드에서도 정적 시트가 보인다(시뮬은 플레이에서만).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GoalNet : MonoBehaviour
{
    [Header("Wedge corners (transform은 원점·단위로 가정)")]
    [SerializeField] private Vector3 frontTopLeft;    // z-, 앞상단(크로스바)
    [SerializeField] private Vector3 frontTopRight;   // z+, 앞상단
    [SerializeField] private Vector3 backBottomLeft;  // z-, 뒤바닥 바
    [SerializeField] private Vector3 backBottomRight; // z+, 뒤바닥 바

    [Header("Grid")]
    [SerializeField] private int cols = 12; // Z 방향 분할
    [SerializeField] private int rows = 8;  // 슬랜트(앞상단→뒤바닥) 분할

    [Header("Sim")]
    [SerializeField] private Vector3 gravity = new Vector3(0f, -2.5f, 0f);
    [SerializeField, Range(0f, 1f)] private float damping = 0.97f;
    [SerializeField] private int iterations = 4;
    [SerializeField, Range(0f, 1f)] private float stiffness = 1f;

    [Header("Ball")]
    [SerializeField] private Transform ball;
    [SerializeField] private float ballRadius = 0.25f;
    [SerializeField] private float ballMargin = 0.12f;

    private Mesh mesh;
    private Vector3[] pos, prev, init;
    private bool[] pinned;
    private int NC, NR, N;

    private void OnEnable() { Build(); }

    private void OnValidate() { if (isActiveAndEnabled) Build(); }

    /// <summary>외부(빌드 코드)에서 코너 설정 후 재생성.</summary>
    public void Rebuild() { Build(); }

    private void Build()
    {
        NC = cols + 1; NR = rows + 1; N = NC * NR;
        pos = new Vector3[N]; prev = new Vector3[N]; init = new Vector3[N]; pinned = new bool[N];

        for (int r = 0; r < NR; r++)
        {
            float v = (float)r / rows;
            for (int c = 0; c < NC; c++)
            {
                float u = (float)c / cols;
                Vector3 top = Vector3.Lerp(frontTopLeft, frontTopRight, u);
                Vector3 bot = Vector3.Lerp(backBottomLeft, backBottomRight, u);
                Vector3 p = Vector3.Lerp(top, bot, v);
                int i = r * NC + c;
                pos[i] = p; prev[i] = p; init[i] = p;
                pinned[i] = (r == 0 || r == rows || c == 0 || c == cols); // 가장자리 핀
            }
        }

        var tris = new List<int>(rows * cols * 6);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int i = r * NC + c;
                tris.Add(i); tris.Add(i + NC); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + NC); tris.Add(i + NC + 1);
            }
        var uv = new Vector2[N];
        for (int r = 0; r < NR; r++)
            for (int c = 0; c < NC; c++)
                uv[r * NC + c] = new Vector2((float)c / cols, (float)r / rows);

        if (mesh == null) { mesh = new Mesh(); mesh.name = "GoalNetMesh"; mesh.MarkDynamic(); }
        mesh.Clear();
        mesh.vertices = pos; mesh.triangles = tris.ToArray(); mesh.uv = uv;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        if (ball == null) { var b = GameObject.Find("Ball"); if (b != null) ball = b.transform; }
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying) return;      // 시뮬은 플레이에서만
        if (pos == null || mesh == null) Build();

        float dt = Time.fixedDeltaTime;
        Vector3 g = gravity * dt * dt;

        // Verlet 적분
        for (int i = 0; i < N; i++)
        {
            if (pinned[i]) { pos[i] = init[i]; prev[i] = init[i]; continue; }
            Vector3 tmp = pos[i];
            pos[i] += (pos[i] - prev[i]) * damping + g;
            prev[i] = tmp;
        }

        // 거리 제약 (구조 이웃: 오른쪽/아래)
        for (int it = 0; it < iterations; it++)
        {
            for (int r = 0; r < NR; r++)
                for (int c = 0; c < NC; c++)
                {
                    int i = r * NC + c;
                    if (c < cols) Constrain(i, i + 1);
                    if (r < rows) Constrain(i, i + NC);
                }
        }

        // 공 충돌: 근처 정점을 공 표면 밖으로
        if (ball != null)
        {
            Vector3 bp = ball.position;
            float R = ballRadius + ballMargin;
            for (int i = 0; i < N; i++)
            {
                if (pinned[i]) continue;
                Vector3 to = pos[i] - bp;
                float d = to.magnitude;
                if (d < R && d > 1e-4f) pos[i] = bp + to / d * R;
            }
        }

        for (int i = 0; i < N; i++) if (pinned[i]) pos[i] = init[i];

        mesh.vertices = pos;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void Constrain(int a, int b)
    {
        Vector3 d = pos[b] - pos[a];
        float len = d.magnitude;
        if (len < 1e-5f) return;
        float rest = (init[b] - init[a]).magnitude;
        float diff = (len - rest) / len * 0.5f * stiffness;
        Vector3 off = d * diff;
        if (!pinned[a]) pos[a] += off;
        if (!pinned[b]) pos[b] -= off;
    }
}
