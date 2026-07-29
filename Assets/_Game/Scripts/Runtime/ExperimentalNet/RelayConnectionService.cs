using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>
/// Unity Relay를 통한 인터넷 접속을 담당한다(포트포워딩 불필요).
/// 호스트가 Allocation을 만들어 조인코드를 발급하고, 게스트는 그 코드로 같은 Relay 서버에 붙는다.
///
/// 실제 게임 데이터는 Relay 서버를 경유해 오갈 뿐, 판정은 여전히 호스트가 한다(호스트 권한 모델).
/// UnityTransport에 RelayServerData를 심어두면 이후 StartHost/StartClient는 자동으로 Relay를 탄다.
///
/// 사용 전 Unity 대시보드에서 프로젝트를 연결(Project Settings > Services)하고
/// Relay 서비스를 활성화해야 한다. 익명 로그인으로 플레이어를 식별한다.
/// </summary>
public static class RelayConnectionService
{
    /// <summary>DTLS(암호화 UDP). Relay 기본 권장 연결 방식.</summary>
    public const string ConnectionTypeDtls = "dtls";

    /// <summary>
    /// 사용할 UGS 환경 이름.
    ///
    /// 조인코드는 환경별로 따로 관리되기 때문에, 호스트와 참가자가 서로 다른 환경을 쓰면
    /// 같은 프로젝트인데도 "join code not found"가 난다.
    /// 각자 에디터 설정에 맡기지 않고 여기서 고정해 그런 어긋남을 막는다.
    /// </summary>
    public const string EnvironmentName = "production";

    /// <summary>로그인된 플레이어 ID. 확인용으로 화면에 보여준다.</summary>
    public static string PlayerId =>
        IsReady ? AuthenticationService.Instance.PlayerId : "";

    /// <summary>서비스 초기화 + 익명 로그인이 끝났는지.</summary>
    public static bool IsReady =>
        UnityServices.State == ServicesInitializationState.Initialized &&
        AuthenticationService.Instance != null &&
        AuthenticationService.Instance.IsSignedIn;

    /// <summary>UGS 초기화와 익명 로그인을 (필요할 때만) 수행한다. 여러 번 불러도 안전하다.</summary>
    public static async Task InitializeAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            InitializationOptions options = new InitializationOptions().SetEnvironmentName(EnvironmentName);
            await UnityServices.InitializeAsync(options);
        }

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    /// <summary>
    /// 실패 원인을 화면에 그대로 보여주기 위해 사람이 읽을 수 있는 형태로 만든다.
    /// Relay 오류는 Reason에 실제 원인(코드 없음/권한 등)이 들어 있어 같이 붙인다.
    /// </summary>
    public static string DescribeError(System.Exception error)
    {
        if (error == null) return "";

        if (error is RelayServiceException relayError)
            return $"{relayError.Reason}: {relayError.Message}";

        return $"{error.GetType().Name}: {error.Message}";
    }

    /// <summary>
    /// 호스트로 Relay 방을 만들고 조인코드를 돌려준다. 트랜스포트 설정까지 끝내므로
    /// 호출한 쪽은 반환 후 NetworkManager.StartHost()만 하면 된다.
    /// </summary>
    /// <param name="maxConnections">호스트를 제외한 최대 접속 인원.</param>
    public static async Task<string> CreateAllocationAsync(int maxConnections)
    {
        await InitializeAsync();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        RelayServerEndpoint endpoint = SelectEndpoint(allocation.ServerEndpoints);
        // 호스트는 자기 ConnectionData를 host 몫으로도 그대로 쓴다.
        SetRelayServerData(new RelayServerData(
            endpoint.Host,
            (ushort)endpoint.Port,
            allocation.AllocationIdBytes,
            allocation.ConnectionData,
            allocation.ConnectionData,
            allocation.Key,
            endpoint.Secure,
            endpoint.ConnectionType == RelayServerEndpoint.ConnectionTypeWss));

        return joinCode;
    }

    /// <summary>
    /// 조인코드로 호스트의 Relay 방에 붙는다. 트랜스포트 설정까지 끝내므로
    /// 호출한 쪽은 반환 후 NetworkManager.StartClient()만 하면 된다.
    /// </summary>
    public static async Task JoinAllocationAsync(string joinCode)
    {
        await InitializeAsync();

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(NormalizeJoinCode(joinCode));

        RelayServerEndpoint endpoint = SelectEndpoint(allocation.ServerEndpoints);
        // 게스트는 자신의 ConnectionData와 호스트의 HostConnectionData를 모두 넘긴다.
        SetRelayServerData(new RelayServerData(
            endpoint.Host,
            (ushort)endpoint.Port,
            allocation.AllocationIdBytes,
            allocation.ConnectionData,
            allocation.HostConnectionData,
            allocation.Key,
            endpoint.Secure,
            endpoint.ConnectionType == RelayServerEndpoint.ConnectionTypeWss));
    }

    /// <summary>조인코드는 대소문자 구분이 없고 공백이 섞이기 쉬우므로 정리해서 보낸다.</summary>
    public static string NormalizeJoinCode(string joinCode) =>
        string.IsNullOrEmpty(joinCode) ? string.Empty : joinCode.Trim().ToUpperInvariant();

    /// <summary>DTLS 엔드포인트를 우선 고르고, 없으면 목록의 첫 번째로 대체한다.</summary>
    private static RelayServerEndpoint SelectEndpoint(List<RelayServerEndpoint> endpoints)
    {
        if (endpoints == null || endpoints.Count == 0)
            throw new System.InvalidOperationException("Relay 할당에 사용할 수 있는 엔드포인트가 없습니다.");

        for (int i = 0; i < endpoints.Count; i++)
            if (endpoints[i].ConnectionType == ConnectionTypeDtls)
                return endpoints[i];

        return endpoints[0];
    }

    private static void SetRelayServerData(RelayServerData serverData)
    {
        NetworkManager nm = NetworkManager.Singleton;
        UnityTransport transport = nm != null ? nm.GetComponent<UnityTransport>() : null;
        if (transport == null)
            throw new System.InvalidOperationException("NetworkManager에 UnityTransport가 없어 Relay를 설정할 수 없습니다.");

        transport.SetRelayServerData(serverData);
    }
}
