using UnityEngine;

public class PowerGauge : MonoBehaviour
{
    [SerializeField] private PowerGaugeConfig config;

    private float currentValue;

    public float CurrentValue => currentValue;
    public float Value01 => config == null ? 0f : Mathf.Clamp01(currentValue / config.capacity);
    public bool IsFull => config != null && currentValue >= config.capacity;

    public void Configure(PowerGaugeConfig value)
    {
        config = value;
        currentValue = config == null ? 0f : Mathf.Clamp(currentValue, 0f, config.capacity);
    }

    public void Tick(float deltaTime, bool matchActive)
    {
        if (!matchActive || config == null)
            return;

        AddAmount(config.passiveGainPerSecond * Mathf.Max(0f, deltaTime));
    }

    public bool TryAdd(PowerGaugeGainSource source)
    {
        if (config == null || !config.TryGetGain(source, out float amount))
            return false;

        AddAmount(amount);
        return true;
    }

    public void ResetGauge()
    {
        currentValue = 0f;
    }

    private void Update()
    {
        Tick(Time.deltaTime, GameManager.PlayActive);
    }

    private void AddAmount(float amount)
    {
        if (config == null)
            return;

        currentValue = Mathf.Clamp(currentValue + Mathf.Max(0f, amount), 0f, config.capacity);
    }
}
