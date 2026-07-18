using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// LAN 접속용 간단 HUD (OnGUI). Host / Join(IP 입력) 버튼.
/// NetworkManager.Singleton의 UnityTransport에 접속 정보를 설정하고 호스트/클라이언트를 시작한다.
/// 이후 Relay 모드는 여기에 토글로 추가할 예정.
/// </summary>
public class NetworkHudUI : MonoBehaviour
{
    [Tooltip("접속할 호스트 IP (Join 시 사용). 같은 PC 테스트는 127.0.0.1, LAN은 호스트의 로컬 IP.")]
    [SerializeField] private string ipAddress = "127.0.0.1";

    [Tooltip("포트.")]
    [SerializeField] private ushort port = 7777;

    private void OnGUI()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 260, 220), GUI.skin.box);

        if (!nm.IsClient && !nm.IsServer)
        {
            GUILayout.Label("LAN 멀티");
            GUILayout.Label("호스트 IP:");
            ipAddress = GUILayout.TextField(ipAddress);

            UnityTransport utp = nm.GetComponent<UnityTransport>();

            if (GUILayout.Button("Host (방 만들기)"))
            {
                if (utp != null) utp.SetConnectionData("0.0.0.0", port, "0.0.0.0");
                nm.StartHost();
            }
            if (GUILayout.Button("Join (접속)"))
            {
                if (utp != null) utp.SetConnectionData(ipAddress, port);
                nm.StartClient();
            }
        }
        else
        {
            string role = nm.IsHost ? "HOST" : (nm.IsServer ? "SERVER" : "CLIENT");
            GUILayout.Label(role + "  |  접속 수: " + nm.ConnectedClientsList.Count);
            if (GUILayout.Button("Disconnect"))
                nm.Shutdown();
        }

        GUILayout.EndArea();
    }
}
