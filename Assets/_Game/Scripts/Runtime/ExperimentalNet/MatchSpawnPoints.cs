using UnityEngine;

/// <summary>
/// 팀별 스폰 위치와 골대를 모아두는 씬 설정 컴포넌트.
/// 네트워크 경기에서 서버가 슬롯 구성대로 선수를 배치할 때 참조한다.
///
/// Inspector에 스폰 포인트를 비워두면 팀 진영 라인 위에 균등하게 자동 배치한다
/// (씬을 손대지 않아도 일단 굴러가도록 하기 위한 대비책).
/// </summary>
public class MatchSpawnPoints : MonoBehaviour
{
    public const byte TeamBlue = 0;
    public const byte TeamRed = 1;

    public static MatchSpawnPoints Instance { get; private set; }

    [Header("스폰 포인트 (비우면 자동 배치)")]
    [SerializeField] private Transform[] blueSpawnPoints;
    [SerializeField] private Transform[] redSpawnPoints;

    [Header("골대")]
    [Tooltip("Blue팀이 지키는 골(= Red팀이 공격하는 골).")]
    [SerializeField] private Transform blueGoal;
    [Tooltip("Red팀이 지키는 골(= Blue팀이 공격하는 골).")]
    [SerializeField] private Transform redGoal;

    [Header("자동 배치 설정")]
    [Tooltip("자동 배치 시 팀 진영이 놓이는 z 거리(중앙선 기준). Blue는 -z, Red는 +z.")]
    [SerializeField] private float autoLineDistance = 6f;
    [Tooltip("자동 배치 시 선수 사이 좌우 간격.")]
    [SerializeField] private float autoLateralSpacing = 3f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>팀 안에서 index번째 선수의 스폰 위치/회전. 지정된 포인트가 모자라면 자동 배치로 채운다.</summary>
    public void GetSpawn(byte team, int indexInTeam, out Vector3 position, out Quaternion rotation)
    {
        Transform[] points = team == TeamBlue ? blueSpawnPoints : redSpawnPoints;
        if (points != null && indexInTeam >= 0 && indexInTeam < points.Length && points[indexInTeam] != null)
        {
            position = points[indexInTeam].position;
            rotation = points[indexInTeam].rotation;
            return;
        }

        BuildAutoSpawn(team, indexInTeam, out position, out rotation);
    }

    /// <summary>중앙선 기준으로 좌우로 번갈아 퍼뜨린다: 0 → 가운데, 1 → 오른쪽, 2 → 왼쪽 ...</summary>
    private void BuildAutoSpawn(byte team, int indexInTeam, out Vector3 position, out Quaternion rotation)
    {
        int step = (indexInTeam + 1) / 2;
        float side = (indexInTeam % 2 == 0) ? 1f : -1f;
        float x = step * autoLateralSpacing * side;
        float z = team == TeamBlue ? -autoLineDistance : autoLineDistance;

        position = new Vector3(x, 1f, z);
        rotation = BuildFacingRotation(team, position);
    }

    /// <summary>공격할 골대를 바라보게 한다. 골대가 지정되지 않았으면 중앙(코트 원점)을 향한다.</summary>
    private Quaternion BuildFacingRotation(byte team, Vector3 position)
    {
        Transform target = GetAttackGoal(team);
        Vector3 forward = target != null ? target.position - position : -position;
        forward.y = 0f;

        return forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward.normalized, Vector3.up)
            : Quaternion.identity;
    }

    /// <summary>이 팀이 공격해야 하는(= 상대가 지키는) 골.</summary>
    public Transform GetAttackGoal(byte team) => team == TeamBlue ? redGoal : blueGoal;

    /// <summary>이 팀이 지켜야 하는 골.</summary>
    public Transform GetOwnGoal(byte team) => team == TeamBlue ? blueGoal : redGoal;
}
