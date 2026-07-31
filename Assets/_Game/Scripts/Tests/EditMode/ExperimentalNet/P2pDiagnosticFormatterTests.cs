using NUnit.Framework;

public class P2pDiagnosticFormatterTests
{
    [Test]
    public void SignalSummary_IdentifiesRoleDirectionKindAndPayloadLength()
    {
        string summary = P2pDiagnosticFormatter.Signal(true, "sent", P2pSignalKind.Offer, 420);

        Assert.That(summary, Is.EqualTo("[P2P:Host] Signal sent: Offer (420 chars)."));
    }

    [Test]
    public void IceSummary_ReportsCandidateCountsWithoutCandidateContents()
    {
        string summary = P2pDiagnosticFormatter.IceState(false, "Failed", 2, 2, 2, 0);

        Assert.That(summary, Is.EqualTo("[P2P:Guest] ICE Failed (generated=2, received=2, applied=2, pending=0)."));
    }
}
