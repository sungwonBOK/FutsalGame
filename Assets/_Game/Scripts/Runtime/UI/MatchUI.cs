using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 경기 HUD 표시 담당. 로직은 갖지 않고 GameManager의 공개 상태를 매 프레임 읽어 그리기만 한다.
/// (점수 / 남은 시간 mm:ss / 중앙 메시지: 카운트다운·GOAL!·결과·PAUSED)
/// Time.timeScale=0(일시정지) 중에도 Update는 계속 돌기 때문에 PAUSED 표시가 정상 동작한다.
/// </summary>
public class MatchUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("점수 표시 텍스트 (예: \"2  :  1\").")]
    [SerializeField] private Text scoreText;
    [Tooltip("남은 시간 텍스트 (mm:ss).")]
    [SerializeField] private Text timerText;
    [Tooltip("화면 중앙 큰 텍스트 (카운트다운/GOAL!/결과/PAUSED).")]
    [SerializeField] private Text centerText;

    [Header("Pause Overlay")]
    [Tooltip("일시정지 시 중앙에 표시할 문구.")]
    [SerializeField] private string pauseMessage = "PAUSED\n(ESC: 재개)";

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (scoreText != null)
            scoreText.text = gm.PlayerScore + "  :  " + gm.OpponentScore;

        if (timerText != null)
            timerText.text = FormatTime(gm.TimeRemaining);

        if (centerText != null)
            centerText.text = gm.IsPaused ? pauseMessage : gm.CenterMessage;
    }

    /// <summary>초를 mm:ss로 변환. 올림 처리해 1초 남았을 때 00:01이 보이게 한다.</summary>
    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int m = total / 60;
        int s = total % 60;
        return m.ToString("00") + ":" + s.ToString("00");
    }
}
