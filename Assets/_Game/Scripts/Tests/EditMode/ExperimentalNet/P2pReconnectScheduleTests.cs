using NUnit.Framework;

public class P2pReconnectScheduleTests
{
    [Test]
    public void NextDelay_UsesCappedBackoff()
    {
        P2pReconnectSchedule schedule = new P2pReconnectSchedule();

        Assert.That(schedule.NextDelaySeconds(), Is.EqualTo(1f));
        schedule.RecordAttempt();
        Assert.That(schedule.NextDelaySeconds(), Is.EqualTo(2f));
        schedule.RecordAttempt();
        Assert.That(schedule.NextDelaySeconds(), Is.EqualTo(5f));
        schedule.RecordAttempt();
        Assert.That(schedule.NextDelaySeconds(), Is.EqualTo(8f));
        schedule.RecordAttempt();
        Assert.That(schedule.NextDelaySeconds(), Is.EqualTo(8f));
    }

    [Test]
    public void StatusMessage_ExplainsWhyGuestCannotReconnectAfterHostLeaves()
    {
        Assert.That(
            P2pSessionStatusText.For(P2pSessionStatus.HostUnavailable),
            Is.EqualTo("방장이 나가 재연결할 수 없습니다."));
    }
}
