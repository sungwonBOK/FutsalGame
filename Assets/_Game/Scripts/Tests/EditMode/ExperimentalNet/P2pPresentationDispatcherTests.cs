using NUnit.Framework;
using UnityEngine;

public sealed class P2pPresentationDispatcherTests
{
    [Test]
    public void SameActionId_IsPresentedOnlyOnce()
    {
        GameObject actor = new GameObject("P2pPresentationDispatcherTests");
        try
        {
            P2pPresentationDispatcher dispatcher = actor.AddComponent<P2pPresentationDispatcher>();
            P2pPresentationRequest request = new P2pPresentationRequest(
                actionId: 42,
                action: P2pPresentationAction.Punch,
                attackerOrigin: Vector3.zero);

            Assert.That(dispatcher.TryPresent(request), Is.True);
            Assert.That(dispatcher.TryPresent(request), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void ResolvedAction_IgnoresLateCancel()
    {
        GameObject actor = new GameObject("P2pPresentationDispatcherTests");
        try
        {
            P2pPresentationDispatcher dispatcher = actor.AddComponent<P2pPresentationDispatcher>();
            P2pPresentationRequest request = new P2pPresentationRequest(
                actionId: 43,
                action: P2pPresentationAction.CrossPunch,
                attackerOrigin: Vector3.zero);

            Assert.That(dispatcher.TryPresent(request), Is.True);
            dispatcher.MarkResolved(request.ActionId);

            Assert.That(dispatcher.TryCancel(request.ActionId), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void PunchProfile_IsLocalAndCanFakeBeforeCommit()
    {
        P2pPresentationProfile profile = P2pPresentationProfiles.Get(P2pPresentationAction.Punch);

        Assert.That(profile.ClipStartOffset, Is.EqualTo(0f));
        Assert.That(profile.CanFake, Is.True);
    }

    [Test]
    public void ExistingP2pActions_MapToTheGameplayVisiblePresentationActions()
    {
        Assert.That(P2pPresentationRouting.FromCombat(P2pCombatActionKind.Punch), Is.EqualTo(P2pPresentationAction.Punch));
        Assert.That(P2pPresentationRouting.FromCombat(P2pCombatActionKind.CrossPunch), Is.EqualTo(P2pPresentationAction.CrossPunch));
        Assert.That(P2pPresentationRouting.FromCombat(P2pCombatActionKind.SlideTackle), Is.EqualTo(P2pPresentationAction.Tackle));
        Assert.That(P2pPresentationRouting.FromCombat(P2pCombatActionKind.Grab), Is.EqualTo(P2pPresentationAction.Grab));
        Assert.That(P2pPresentationRouting.FromBall(P2pBallActionKind.Pass), Is.EqualTo(P2pPresentationAction.Pass));
        Assert.That(P2pPresentationRouting.FromBall(P2pBallActionKind.Shot), Is.EqualTo(P2pPresentationAction.Shot));
    }

    [Test]
    public void CombatCancel_UsesTheExistingFixedSizeCombatMessage()
    {
        P2pCombatMessage cancel = new P2pCombatMessage(
            P2pCombatMessageKind.ActionCancel,
            actionId: 44,
            sequence: 3,
            P2pCombatActionKind.Punch,
            P2pCombatResolution.Hit,
            Vector3.zero,
            Vector3.forward);

        Assert.That(P2pCombatCodec.TryEncode(cancel, out byte[] payload), Is.True);
        Assert.That(payload, Has.Length.EqualTo(P2pCombatCodec.PacketSize));
        Assert.That(P2pCombatCodec.TryDecode(payload, out P2pCombatMessage decoded), Is.True);
        Assert.That(decoded.Kind, Is.EqualTo(P2pCombatMessageKind.ActionCancel));
        Assert.That(decoded.ActionId, Is.EqualTo(44));
    }
}
