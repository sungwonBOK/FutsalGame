using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 소유자(클라이언트) 권위 NetworkTransform.
/// 기본 NetworkTransform은 서버 권위라, 내 캐릭터를 내 클라이언트에서 직접 움직이려면 이걸 쓴다.
/// (LAN 친선 게임: 각 플레이어가 자기 캐릭터의 물리를 로컬에서 시뮬레이션하고 결과 위치를 동기화)
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    /// <summary>서버 권위 대신 소유자(오너) 권위로 동작.</summary>
    protected override bool OnIsServerAuthoritative() => false;
}
