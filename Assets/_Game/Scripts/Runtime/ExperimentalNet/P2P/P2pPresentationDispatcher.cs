using System.Collections.Generic;
using UnityEngine;

public enum P2pPresentationAction : byte
{
    Punch = 1,
    CrossPunch = 2,
    Tackle = 3,
    Grab = 4,
    Block = 5,
    Pass = 6,
    Shot = 7
}

public enum P2pPresentationCancelStyle : byte
{
    Immediate = 1,
    BlendOut = 2
}

public readonly struct P2pPresentationProfile
{
    public float ClipStartOffset { get; }
    public bool CanFake { get; }
    public P2pPresentationCancelStyle CancelStyle { get; }

    public P2pPresentationProfile(float clipStartOffset, bool canFake, P2pPresentationCancelStyle cancelStyle)
    {
        ClipStartOffset = Mathf.Max(0f, clipStartOffset);
        CanFake = canFake;
        CancelStyle = cancelStyle;
    }
}

public readonly struct P2pPresentationRequest
{
    public uint ActionId { get; }
    public P2pPresentationAction Action { get; }
    public Vector3 AttackerOrigin { get; }

    public P2pPresentationRequest(uint actionId, P2pPresentationAction action, Vector3 attackerOrigin)
    {
        ActionId = actionId;
        Action = action;
        AttackerOrigin = attackerOrigin;
    }
}

public static class P2pPresentationProfiles
{
    public static P2pPresentationProfile Get(P2pPresentationAction action)
    {
        switch (action)
        {
            case P2pPresentationAction.Punch:
            case P2pPresentationAction.CrossPunch:
            case P2pPresentationAction.Tackle:
            case P2pPresentationAction.Grab:
                return new P2pPresentationProfile(0f, canFake: true, P2pPresentationCancelStyle.BlendOut);
            case P2pPresentationAction.Block:
            case P2pPresentationAction.Pass:
            case P2pPresentationAction.Shot:
                return new P2pPresentationProfile(0f, canFake: false, P2pPresentationCancelStyle.Immediate);
            default:
                return default;
        }
    }
}

public static class P2pPresentationRouting
{
    public static P2pPresentationAction FromCombat(P2pCombatActionKind action)
    {
        switch (action)
        {
            case P2pCombatActionKind.Punch: return P2pPresentationAction.Punch;
            case P2pCombatActionKind.CrossPunch: return P2pPresentationAction.CrossPunch;
            case P2pCombatActionKind.SlideTackle: return P2pPresentationAction.Tackle;
            default: return P2pPresentationAction.Grab;
        }
    }

    public static P2pPresentationAction FromBall(P2pBallActionKind action)
    {
        return action == P2pBallActionKind.Pass || action == P2pBallActionKind.LobPass
            ? P2pPresentationAction.Pass
            : P2pPresentationAction.Shot;
    }
}

/// <summary>
/// Keeps local cross-peer presentation idempotent. Gameplay state and P2P authority remain with
/// their existing combat and ball receivers.
/// </summary>
public sealed class P2pPresentationDispatcher : MonoBehaviour
{
    private readonly Dictionary<uint, P2pPresentationProfile> presented = new Dictionary<uint, P2pPresentationProfile>();
    private readonly HashSet<uint> resolved = new HashSet<uint>();
    private readonly HashSet<uint> cancelled = new HashSet<uint>();
    private CombatController combat;
    private PlayerBallHandler ballHandler;

    private void Awake()
    {
        combat = GetComponent<CombatController>();
        ballHandler = GetComponent<PlayerBallHandler>();
    }

    public bool TryPresent(P2pPresentationRequest request)
    {
        if (request.ActionId == 0 || presented.ContainsKey(request.ActionId) || cancelled.Contains(request.ActionId))
            return false;

        P2pPresentationProfile profile = P2pPresentationProfiles.Get(request.Action);
        presented.Add(request.ActionId, profile);
        Play(request, profile);
        return true;
    }

    public void MarkResolved(uint actionId)
    {
        if (actionId != 0)
            resolved.Add(actionId);
    }

    public bool TryCancel(uint actionId)
    {
        if (!presented.TryGetValue(actionId, out P2pPresentationProfile profile)
            || resolved.Contains(actionId)
            || !profile.CanFake)
        {
            return false;
        }

        cancelled.Add(actionId);
        combat?.CancelP2pPresentation(profile.CancelStyle);
        return true;
    }

    private void Play(P2pPresentationRequest request, P2pPresentationProfile profile)
    {
        switch (request.Action)
        {
            case P2pPresentationAction.Punch:
            case P2pPresentationAction.CrossPunch:
            case P2pPresentationAction.Tackle:
            case P2pPresentationAction.Grab:
            case P2pPresentationAction.Block:
                combat?.PlayP2pPresentation(request.Action, profile, request.AttackerOrigin);
                break;
            case P2pPresentationAction.Pass:
            case P2pPresentationAction.Shot:
                ballHandler?.PlayP2pPresentation(request.Action, profile);
                break;
        }
    }
}
