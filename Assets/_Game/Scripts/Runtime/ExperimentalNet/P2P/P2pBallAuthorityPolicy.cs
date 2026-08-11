/// <summary>
/// Pure ordering rules for the direct-P2P ball authority timeline.
/// A dribbling ball has one owner and that owner is also the authority.
/// </summary>
public struct P2pBallAuthorityState
{
    private readonly ulong authorityId;
    private readonly ulong ownerId;
    private readonly uint epoch;

    public ulong AuthorityId { get { return authorityId; } }
    public ulong OwnerId { get { return ownerId; } }
    public uint Epoch { get { return epoch; } }

    public P2pBallAuthorityState(ulong authorityId, ulong ownerId, uint epoch)
    {
        this.authorityId = authorityId;
        this.ownerId = ownerId;
        this.epoch = epoch;
    }
}

public enum P2pBallAuthorityTransitionKind : byte
{
    NoBallTransfer = 0,
    TackleWon = 1
}

public struct P2pBallAuthorityTransition
{
    private readonly P2pBallAuthorityTransitionKind kind;
    private readonly P2pBallAuthorityState state;

    public P2pBallAuthorityTransitionKind Kind { get { return kind; } }
    public P2pBallAuthorityState State { get { return state; } }

    public P2pBallAuthorityTransition(
        P2pBallAuthorityTransitionKind kind,
        P2pBallAuthorityState state)
    {
        this.kind = kind;
        this.state = state;
    }
}

public static class P2pBallAuthorityPolicy
{
    /// <summary>
    /// Direct-P2P ball authority executes the owner's action locally and replicates the
    /// committed ball state. The legacy NGO host RPC is only the fallback transport.
    /// </summary>
    public static bool ShouldForwardOwnerActionToServer(
        bool isNetworked,
        bool isOwner,
        bool isServer,
        bool isDirectP2pBallAuthorityActive)
    {
        return isNetworked
            && isOwner
            && !isServer
            && !isDirectP2pBallAuthorityActive;
    }

    public static P2pBallAuthorityTransition ResolveTackle(
        P2pBallAuthorityState current,
        ulong attackerId,
        P2pCombatResolution resolution)
    {
        if (resolution != P2pCombatResolution.Hit
            || attackerId == 0
            || current.OwnerId == 0)
        {
            return new P2pBallAuthorityTransition(
                P2pBallAuthorityTransitionKind.NoBallTransfer,
                current);
        }

        P2pBallAuthorityState wonBall = new P2pBallAuthorityState(
            attackerId,
            0,
            current.Epoch + 1);
        return new P2pBallAuthorityTransition(
            P2pBallAuthorityTransitionKind.TackleWon,
            wonBall);
    }

    public static bool ShouldAcceptSnapshot(
        P2pBallAuthorityState current,
        ushort lastAcceptedSequence,
        ulong snapshotAuthorityId,
        uint snapshotEpoch,
        ushort snapshotSequence)
    {
        return current.AuthorityId == snapshotAuthorityId
            && current.Epoch == snapshotEpoch
            && IsNewer(snapshotSequence, lastAcceptedSequence);
    }

    public static bool TryApplyAuthorityTransfer(
        P2pBallAuthorityState current,
        ulong sourceAuthorityId,
        ulong nextAuthorityId,
        ulong nextOwnerId,
        uint nextEpoch,
        out P2pBallAuthorityState next)
    {
        next = current;
        if (sourceAuthorityId == 0
            || nextAuthorityId == 0
            || sourceAuthorityId != current.AuthorityId
            || nextEpoch != current.Epoch + 1)
        {
            return false;
        }

        next = new P2pBallAuthorityState(nextAuthorityId, nextOwnerId, nextEpoch);
        return true;
    }

    /// <summary>
    /// Accepts a takeover after the current authority's direct link failed.
    /// The elected surviving peer becomes authority, but the ball stays free.
    /// This is intentionally narrower than a normal transfer: it requires the
    /// locally recorded disconnected authority and one epoch advance.
    /// </summary>
    public static bool TryApplyPeerDisconnectTransfer(
        P2pBallAuthorityState current,
        ulong disconnectedAuthorityId,
        ulong sourceAuthorityId,
        ulong nextAuthorityId,
        ulong nextOwnerId,
        uint nextEpoch,
        out P2pBallAuthorityState next)
    {
        next = current;
        if (disconnectedAuthorityId == 0
            || disconnectedAuthorityId != current.AuthorityId
            || sourceAuthorityId == 0
            || sourceAuthorityId != nextAuthorityId
            || nextAuthorityId == 0
            || nextOwnerId != 0
            || nextEpoch != current.Epoch + 1)
        {
            return false;
        }

        next = new P2pBallAuthorityState(nextAuthorityId, nextOwnerId, nextEpoch);
        return true;
    }

    private static bool IsNewer(ushort candidate, ushort current)
    {
        ushort difference = (ushort)(candidate - current);
        return difference != 0 && difference < 32768;
    }
}
