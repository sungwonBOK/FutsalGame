using NUnit.Framework;
using UnityEngine;

public class DefenseWindowTests
{
    [Test]
    public void DefenseWindow_BlocksOnlyDuringHalfSecondWindowAndSelectsTheIncomingAttackArc()
    {
        Assert.That(TryBlockAt(11.49f, new Vector3(1f, 0f, 1f), out string rightDirection), Is.True);
        Assert.That(rightDirection, Is.EqualTo("Right"));

        Assert.That(TryBlockAt(11.49f, Vector3.back, out string backDirection), Is.True);
        Assert.That(backDirection, Is.EqualTo("Back"));

        Assert.That(TryBlockAt(11.49f, new Vector3(-1f, 0f, 1f), out string leftDirection), Is.True);
        Assert.That(leftDirection, Is.EqualTo("Left"));

        Assert.That(TryBlockAt(11.5f, Vector3.forward, out _), Is.False);
    }

    private static bool TryBlockAt(float now, Vector3 attackerPosition, out string direction)
    {
        DefenseWindow window = new DefenseWindow();
        window.Begin(10f);
        bool blocked = window.TryBlock(
            now,
            Vector3.zero,
            Vector3.forward,
            attackerPosition,
            out DefenseBlockDirection blockDirection);
        direction = blockDirection.ToString();
        return blocked;
    }
}
