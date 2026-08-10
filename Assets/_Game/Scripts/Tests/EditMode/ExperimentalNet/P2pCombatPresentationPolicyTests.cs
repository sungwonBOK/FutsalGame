using System.Reflection;
using NUnit.Framework;

public sealed class P2pCombatPresentationPolicyTests
{
    [Test]
    public void BlockResult_ReplaysTheDefendersBlockPresentationForTheAttacker()
    {
        MethodInfo shouldReplay = typeof(P2pCombatCodec).Assembly
            .GetType("P2pCombatPresentationPolicy")
            ?.GetMethod("ShouldReplayRemoteBlock", BindingFlags.Public | BindingFlags.Static);

        Assert.That(shouldReplay, Is.Not.Null, "P2P block results need a remote presentation rule.");
        Assert.That((bool)shouldReplay.Invoke(null, new object[] { P2pCombatResolution.Block }), Is.True);
    }
}
