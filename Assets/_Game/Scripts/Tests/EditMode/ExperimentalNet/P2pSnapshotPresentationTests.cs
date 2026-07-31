using NUnit.Framework;
using UnityEngine;

public class P2pSnapshotPresentationTests
{
    [Test]
    public void SnapshotPresentation_InterpolatesPositionAndYaw()
    {
        P2pPlayerSnapshot snapshot = new P2pPlayerSnapshot(1, new Vector3(10f, 0f, 0f), 90f);

        P2pSnapshotPresentation.Step(
            Vector3.zero,
            0f,
            snapshot,
            0.5f,
            out Vector3 position,
            out float yawDegrees);

        Assert.That(position, Is.EqualTo(new Vector3(5f, 0f, 0f)));
        Assert.That(yawDegrees, Is.EqualTo(45f));
    }
}
