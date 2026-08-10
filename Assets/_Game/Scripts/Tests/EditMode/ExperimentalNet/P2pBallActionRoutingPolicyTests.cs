using System.Reflection;
using NUnit.Framework;

public sealed class P2pBallActionRoutingPolicyTests
{
    [Test]
    public void DirectP2pGuestBallAction_DoesNotRouteThroughHostRpc()
    {
        MethodInfo route = typeof(P2pBallAuthorityPolicy).GetMethod(
            "ShouldForwardOwnerActionToServer",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(route, Is.Not.Null, "Direct-P2P ball actions need an explicit routing rule.");
        Assert.That((bool)route.Invoke(null, new object[] { true, true, false, true }), Is.False);
    }
}
