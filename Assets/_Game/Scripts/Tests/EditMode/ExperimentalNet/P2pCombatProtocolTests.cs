using NUnit.Framework;
using UnityEngine;

public sealed class P2pCombatProtocolTests
{
    [Test]
    public void Codec_RoundTripsPowerStunInteraction()
    {
        P2pCombatActionKind powerStun = (P2pCombatActionKind)System.Enum.Parse(
            typeof(P2pCombatActionKind), "PowerStun");
        P2pCombatMessage source = new P2pCombatMessage(
            P2pCombatMessageKind.InteractionRequest,
            actionId: 44,
            sequence: 7,
            actionKind: powerStun,
            resolution: P2pCombatResolution.Hit,
            origin: new Vector3(1f, 0f, 2f),
            direction: Vector3.forward);

        Assert.That(P2pCombatCodec.TryEncode(source, out byte[] payload), Is.True);
        Assert.That(P2pCombatCodec.TryDecode(payload, out P2pCombatMessage decoded), Is.True);
        Assert.That(decoded.ActionKind, Is.EqualTo(powerStun));
        Assert.That(decoded.ActionId, Is.EqualTo(44));
    }
}
