using NUnit.Framework;

public class CharacterAnimatorGrabOffsetTests
{
    [Test]
    public void CalculateGrabVerticalOffset_RestoresTheBaselineHipHeight()
    {
        float offset = CharacterAnimator.CalculateGrabVerticalOffset(1.145f, 0.102f);

        Assert.That(offset, Is.EqualTo(1.043f).Within(0.001f));
    }
}
