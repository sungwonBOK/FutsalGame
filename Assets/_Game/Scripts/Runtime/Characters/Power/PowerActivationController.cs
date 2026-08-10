using UnityEngine;

[RequireComponent(typeof(PowerGauge))]
public class PowerActivationController : MonoBehaviour
{
    private readonly PowerActivationState state = new PowerActivationState();
    private PowerGauge gauge;

    public bool IsArmed => state.IsArmed;

    private void Awake()
    {
        gauge = GetComponent<PowerGauge>();
    }

    public bool TryArm()
    {
        EnsureGauge();
        return state.TryArm(gauge != null && gauge.IsFull);
    }

    public bool TryCancel()
    {
        return state.TryCancel();
    }

    public bool TryConsume(EnhancedActionKind action, bool wasAccepted)
    {
        if (!state.TryConsume(action, wasAccepted))
            return false;

        EnsureGauge();
        gauge?.ResetGauge();
        return true;
    }

    public void ResetForNewMatch()
    {
        state.Reset();
        EnsureGauge();
        gauge?.ResetGauge();
    }

    private void EnsureGauge()
    {
        if (gauge == null)
            gauge = GetComponent<PowerGauge>();
    }
}
