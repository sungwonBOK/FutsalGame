using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 온라인 경기에 필요한 씬/프리팹 배선을 한 번에 해주는 에디터 도구.
/// 손으로 컴포넌트를 붙이고 참조를 끌어다 놓는 과정에서 나기 쉬운 실수를 막는다.
///
/// 하는 일:
///  1) NetPlayer 프리팹에 NetworkPlayerAgent를 붙인다.
///  2) 열려 있는 씬에 MatchSpawnPoints / MatchSpawner 오브젝트를 만들고 참조를 채운다.
///
/// 여러 번 실행해도 안전하다(이미 있으면 건드리지 않는다).
/// </summary>
public static class NetworkMatchSetupTool
{
    private const string NetPlayerPrefabPath = "Assets/_Game/Prefabs/NetPlayer.prefab";

    [MenuItem("Futsal/온라인 경기 배선 설정")]
    public static void SetupOnlineMatch()
    {
        GameObject playerPrefab = SetupNetPlayerPrefab();
        SetupSceneObjects(playerPrefab);
        SetupBall();
        SetupMatchState();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[NetworkMatchSetupTool] 온라인 경기 배선을 마쳤습니다. 씬을 저장하세요.");
    }

    /// <summary>NetPlayer 프리팹에 네트워크 경기에 필요한 컴포넌트를 보장한다.</summary>
    private static GameObject SetupNetPlayerPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetPlayerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[NetworkMatchSetupTool] 프리팹을 찾을 수 없습니다: {NetPlayerPrefabPath}");
            return null;
        }

        if (prefab.GetComponent<NetworkObject>() == null)
            Debug.LogWarning("[NetworkMatchSetupTool] NetPlayer에 NetworkObject가 없습니다. 네트워크 스폰이 불가능합니다.", prefab);

        bool needsAgent = prefab.GetComponent<NetworkPlayerAgent>() == null;
        // AI 슬롯으로 스폰될 수 있으므로 AI 컨트롤러도 프리팹에 있어야 한다.
        // 실제로 켤지는 NetworkPlayerAgent가 팀/AI 여부를 보고 결정한다.
        bool needsAI = prefab.GetComponent<SimpleAIController>() == null;
        if (!needsAgent && !needsAI)
            return prefab;

        GameObject root = PrefabUtility.LoadPrefabContents(NetPlayerPrefabPath);
        if (needsAgent) root.AddComponent<NetworkPlayerAgent>();
        if (needsAI) root.AddComponent<SimpleAIController>().enabled = false;
        PrefabUtility.SaveAsPrefabAsset(root, NetPlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log($"[NetworkMatchSetupTool] NetPlayer 프리팹 갱신 " +
                  $"(NetworkPlayerAgent: {(needsAgent ? "추가" : "이미 있음")}, " +
                  $"SimpleAIController: {(needsAI ? "추가" : "이미 있음")})");

        return AssetDatabase.LoadAssetAtPath<GameObject>(NetPlayerPrefabPath);
    }

    /// <summary>씬에 스폰 포인트/스포너를 만들고 골대·오프라인 캐릭터 참조를 채운다.</summary>
    private static void SetupSceneObjects(GameObject playerPrefab)
    {
        MatchSpawnPoints spawnPoints = Object.FindAnyObjectByType<MatchSpawnPoints>();
        if (spawnPoints == null)
        {
            spawnPoints = new GameObject("MatchSpawnPoints").AddComponent<MatchSpawnPoints>();
            Undo.RegisterCreatedObjectUndo(spawnPoints.gameObject, "Create MatchSpawnPoints");
        }

        // Blue(플레이어 진영)는 West 골을 지키고 East 골을 공격한다 — GoalTrigger 설정과 일치시킨다.
        SerializedObject spawnPointsSo = new SerializedObject(spawnPoints);
        AssignIfEmpty(spawnPointsSo.FindProperty("blueGoal"), FindTransform("Goal_West"));
        AssignIfEmpty(spawnPointsSo.FindProperty("redGoal"), FindTransform("Goal_East"));
        spawnPointsSo.ApplyModifiedProperties();

        // 접속 실패 원인을 화면에 설명해주는 컴포넌트. 로비와 같은 오브젝트에 둔다.
        if (Object.FindAnyObjectByType<NetworkConnectionReporter>() == null)
        {
            LobbyController lobby = Object.FindAnyObjectByType<LobbyController>();
            GameObject host = lobby != null
                ? lobby.gameObject
                : new GameObject("NetworkConnectionReporter");

            if (lobby == null)
                Undo.RegisterCreatedObjectUndo(host, "Create NetworkConnectionReporter");

            Undo.AddComponent<NetworkConnectionReporter>(host);
            EditorUtility.SetDirty(host);
        }

        MatchSpawner spawner = Object.FindAnyObjectByType<MatchSpawner>();
        if (spawner == null)
        {
            spawner = new GameObject("MatchSpawner").AddComponent<MatchSpawner>();
            Undo.RegisterCreatedObjectUndo(spawner.gameObject, "Create MatchSpawner");
        }

        SerializedObject spawnerSo = new SerializedObject(spawner);

        SerializedProperty prefabProperty = spawnerSo.FindProperty("playerPrefab");
        if (prefabProperty.objectReferenceValue == null && playerPrefab != null)
            prefabProperty.objectReferenceValue = playerPrefab.GetComponent<NetworkObject>();

        // 온라인 경기가 시작되면 꺼야 할, 씬에 고정 배치된 캐릭터들.
        SerializedProperty offlineObjects = spawnerSo.FindProperty("offlineOnlyObjects");
        if (offlineObjects.arraySize == 0)
        {
            GameObject player = GameObject.Find("Player");
            GameObject opponent = GameObject.Find("Opponent");
            AppendIfPresent(offlineObjects, player);
            AppendIfPresent(offlineObjects, opponent);
        }

        spawnerSo.ApplyModifiedProperties();
    }

    /// <summary>
    /// 씬의 공을 네트워크로 복제되게 만든다.
    /// 위치는 서버 권한 NetworkTransform이 복제하고, NetworkRigidbody가 클라 쪽 물리를 꺼준다
    /// (안 그러면 클라에서 굴러가는 공과 복제된 위치가 서로 싸운다).
    /// </summary>
    private static void SetupBall()
    {
        BallController ball = Object.FindAnyObjectByType<BallController>();
        if (ball == null)
        {
            Debug.LogWarning("[NetworkMatchSetupTool] 씬에서 공(BallController)을 찾지 못했습니다.");
            return;
        }

        GameObject ballObject = ball.gameObject;
        EnsureComponent<NetworkObject>(ballObject);
        EnsureComponent<NetworkTransform>(ballObject);   // 기본값이 서버 권한이다
        EnsureComponent<NetworkRigidbody>(ballObject);
        EnsureComponent<NetworkBall>(ballObject);

        EditorUtility.SetDirty(ballObject);
        Debug.Log("[NetworkMatchSetupTool] 공에 네트워크 컴포넌트를 확인/추가했습니다.", ballObject);
    }

    /// <summary>
    /// 경기 상태(점수·시간·킥오프)를 복제하는 컴포넌트를 GameManager에 붙인다.
    /// 씬에 원래 있는 오브젝트이므로 in-scene NetworkObject로 스폰된다.
    /// </summary>
    private static void SetupMatchState()
    {
        GameManager match = Object.FindAnyObjectByType<GameManager>();
        if (match == null)
        {
            Debug.LogWarning("[NetworkMatchSetupTool] 씬에서 GameManager를 찾지 못했습니다.");
            return;
        }

        GameObject matchObject = match.gameObject;
        EnsureComponent<NetworkObject>(matchObject);
        EnsureComponent<NetworkMatchState>(matchObject);

        EditorUtility.SetDirty(matchObject);
        Debug.Log("[NetworkMatchSetupTool] GameManager에 경기 상태 복제 컴포넌트를 확인/추가했습니다.", matchObject);
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        if (existing != null) return existing;

        return Undo.AddComponent<T>(target);
    }

    private static void AssignIfEmpty(SerializedProperty property, Object value)
    {
        if (property != null && property.objectReferenceValue == null && value != null)
            property.objectReferenceValue = value;
    }

    private static void AppendIfPresent(SerializedProperty arrayProperty, GameObject value)
    {
        if (value == null) return;

        int index = arrayProperty.arraySize;
        arrayProperty.InsertArrayElementAtIndex(index);
        arrayProperty.GetArrayElementAtIndex(index).objectReferenceValue = value;
    }

    private static Transform FindTransform(string name)
    {
        GameObject found = GameObject.Find(name);
        return found != null ? found.transform : null;
    }
}
