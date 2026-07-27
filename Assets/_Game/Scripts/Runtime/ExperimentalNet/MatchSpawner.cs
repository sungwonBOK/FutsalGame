using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 로비에서 구성한 팀 슬롯대로 실제 선수를 네트워크 스폰한다(서버 전용 로직).
///
/// 사람 슬롯은 해당 클라이언트 소유로 스폰해서 그 사람이 직접 조종하게 하고,
/// AI 슬롯은 서버 소유로 스폰해서 서버가 판단을 돌린다.
/// 스폰된 선수의 팀/AI 여부는 NetworkPlayerAgent가 복제한다.
///
/// 오프라인 씬에 미리 놓여 있는 Player/Opponent는 네트워크 경기가 시작되면 꺼서
/// 스폰된 선수들과 섞이지 않게 한다.
/// </summary>
public class MatchSpawner : MonoBehaviour
{
    public static MatchSpawner Instance { get; private set; }

    [Header("스폰")]
    [Tooltip("네트워크로 스폰할 선수 프리팹(NetworkObject + NetworkPlayerAgent 필요).")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("오프라인 전용")]
    [Tooltip("네트워크 경기 시작 시 끌 오브젝트(씬에 고정 배치된 Player/Opponent 등).")]
    [SerializeField] private GameObject[] offlineOnlyObjects;

    private readonly List<NetworkObject> spawnedPlayers = new List<NetworkObject>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>슬롯 구성대로 선수를 스폰한다. 서버에서만 호출된다.</summary>
    public void ServerSpawnTeams(IReadOnlyList<TeamSlot> slots)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        if (playerPrefab == null)
        {
            Debug.LogError("[MatchSpawner] playerPrefab이 지정되지 않아 선수를 스폰할 수 없습니다.", this);
            return;
        }

        DespawnAll();
        PrepareForNetworkMatch();

        // 팀별로 몇 번째 선수인지 세어 스폰 위치를 나눠준다.
        int blueCount = 0;
        int redCount = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            TeamSlot slot = slots[i];
            if (slot.type == Occupant.Empty) continue;

            int indexInTeam = slot.team == MatchSpawnPoints.TeamBlue ? blueCount++ : redCount++;
            SpawnOne(slot, indexInTeam);
        }

        RefreshAIOpponents();
    }

    /// <summary>
    /// 선수들은 하나씩 스폰되므로 먼저 스폰된 AI는 나중에 온 선수를 모른다.
    /// 전부 스폰된 뒤 한 번 더 상대 목록을 잡아준다.
    /// </summary>
    private void RefreshAIOpponents()
    {
        for (int i = 0; i < spawnedPlayers.Count; i++)
        {
            NetworkObject instance = spawnedPlayers[i];
            if (instance == null) continue;

            SimpleAIController ai = instance.GetComponent<SimpleAIController>();
            if (ai != null && ai.enabled)
                ai.RefreshOpponents();
        }
    }

    private void SpawnOne(TeamSlot slot, int indexInTeam)
    {
        Vector3 position;
        Quaternion rotation;
        if (MatchSpawnPoints.Instance != null)
        {
            MatchSpawnPoints.Instance.GetSpawn(slot.team, indexInTeam, out position, out rotation);
        }
        else
        {
            position = new Vector3(indexInTeam * 2f, 1f, slot.team == MatchSpawnPoints.TeamBlue ? -6f : 6f);
            rotation = Quaternion.LookRotation(slot.team == MatchSpawnPoints.TeamBlue ? Vector3.forward : Vector3.back);
        }

        NetworkObject instance = Instantiate(playerPrefab, position, rotation);

        bool isAI = slot.type == Occupant.AI;
        NetworkPlayerAgent agent = instance.GetComponent<NetworkPlayerAgent>();
        if (agent != null)
            agent.ServerPrepare(slot.team, isAI);
        else
            Debug.LogWarning("[MatchSpawner] 프리팹에 NetworkPlayerAgent가 없어 팀/AI 설정을 건너뜁니다.", instance);

        if (isAI)
            instance.Spawn();                              // 서버 소유 = 서버가 AI를 돌린다
        else
            instance.SpawnAsPlayerObject(slot.clientId);   // 해당 클라 소유 = 그 사람이 조종한다

        spawnedPlayers.Add(instance);
    }

    /// <summary>이전 경기에서 스폰된 선수를 모두 정리한다(재시작 대비).</summary>
    public void DespawnAll()
    {
        for (int i = 0; i < spawnedPlayers.Count; i++)
        {
            NetworkObject instance = spawnedPlayers[i];
            if (instance != null && instance.IsSpawned)
                instance.Despawn(destroy: true);
        }
        spawnedPlayers.Clear();
    }

    /// <summary>
    /// 씬에 고정 배치된 오프라인 캐릭터를 끈다. 스폰된 선수와 섞이지 않게 하려는 것이므로
    /// 서버뿐 아니라 모든 클라이언트에서 호출해야 한다.
    /// </summary>
    public void PrepareForNetworkMatch()
    {
        if (offlineOnlyObjects == null) return;
        foreach (GameObject target in offlineOnlyObjects)
        {
            if (target != null && target.activeSelf)
                target.SetActive(false);
        }
    }
}
