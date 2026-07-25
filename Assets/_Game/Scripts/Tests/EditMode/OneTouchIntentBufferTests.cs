using NUnit.Framework;

public class OneTouchIntentBufferTests
{
    [Test]
    public void Queue_ReplacesPriorIntentAndMarksPreparation()
    {
        var buffer = new OneTouchIntentBuffer();

        buffer.Queue(OneTouchIntent.Pass);
        buffer.Queue(OneTouchIntent.Shot);

        Assert.That(buffer.Intent, Is.EqualTo(OneTouchIntent.Shot));
        Assert.That(buffer.IsPreparing, Is.True);
    }

    [Test]
    public void ClearAndConsume_RemoveTheQueuedIntent()
    {
        var buffer = new OneTouchIntentBuffer();
        buffer.Queue(OneTouchIntent.Pass);

        Assert.That(buffer.Consume(), Is.EqualTo(OneTouchIntent.Pass));
        Assert.That(buffer.IsPreparing, Is.False);

        buffer.Queue(OneTouchIntent.Shot);
        buffer.Clear();

        Assert.That(buffer.Intent, Is.EqualTo(OneTouchIntent.None));
    }

    [Test]
    public void ExecuteQueued_WithoutAHandler_KeepsTheIntentPrepared()
    {
        var buffer = new OneTouchIntentBuffer();
        var executor = new OneTouchActionExecutor();
        buffer.Queue(OneTouchIntent.Pass);

        bool executed = executor.TryExecuteQueued(buffer, null, UnityEngine.Vector3.forward);

        Assert.That(executed, Is.False);
        Assert.That(buffer.Intent, Is.EqualTo(OneTouchIntent.Pass));
    }
}
