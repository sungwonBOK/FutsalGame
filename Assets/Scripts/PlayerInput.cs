using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 사람 플레이어 입력 → 몸(CharacterMotor / CombatController / PlayerBallHandler)을 구동한다.
/// 이 컴포넌트만 상대(더미/AI)에 붙이지 않으면, 완전히 동일한 몸을 AI가 대신 구동할 수 있다.
/// WASD/화살표=이동, J=펀치, K=슬라이딩, Space=슛.
/// </summary>
[RequireComponent(typeof(CharacterMotor))]
public class PlayerInput : MonoBehaviour
{
    private CharacterMotor motor;
    private CombatController combat;
    private PlayerBallHandler ball;
    private CharacterState state;

    private void Awake()
    {
        motor = GetComponent<CharacterMotor>();
        combat = GetComponent<CombatController>();
        ball = GetComponent<PlayerBallHandler>();
        state = GetComponent<CharacterState>();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // 킥오프 대기/경기 종료 중엔 입력 정지.
        if (!GameManager.PlayActive)
        {
            motor.SetMoveInput(Vector3.zero);
            return;
        }

        // 기절 중엔 입력 무시(이동 정지).
        if (state != null && state.IsStunned)
        {
            motor.SetMoveInput(Vector3.zero);
            return;
        }

        float h = 0f, v = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
        motor.SetMoveInput(new Vector3(h, 0f, v));

        if (kb.jKey.wasPressedThisFrame && combat != null) combat.Punch();
        if (kb.kKey.wasPressedThisFrame && combat != null) combat.SlideTackle();

        // 스페이스: 눌러서 차징 시작 → 떼면 차징한 만큼 세기로 발사. 짧게 탭=약한 패스, 길게 홀드=강한 슛.
        if (ball != null)
        {
            if (kb.spaceKey.wasPressedThisFrame) ball.StartCharge();
            if (kb.spaceKey.wasReleasedThisFrame) ball.ReleaseCharge();
        }
    }
}
