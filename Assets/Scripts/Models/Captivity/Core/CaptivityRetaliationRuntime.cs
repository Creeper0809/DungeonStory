using UnityEngine;

public static class CaptivityRetaliationPolicy
{
    public static float RansomPressure(CaptiveRansomedEvent gameEvent) =>
        Mathf.Max(0f, gameEvent.RetaliationPressure);

    public static float EscapePressure(
        CaptiveEscapedEvent gameEvent,
        CaptiveState captive)
    {
        float pressure = gameEvent.Betrayal ? 45f : 28f;
        if (captive != null)
        {
            pressure += captive.retaliationPressure * 0.55f;
            pressure += captive.grudge * 0.2f;
        }
        return pressure;
    }

    public static float Clamp(float pressure) => Mathf.Clamp(pressure, 0f, 100f);
    public static float ThreatGain(float pressure) => Clamp(pressure) * 0.45f;
    internal static bool IsHigh(float pressure) => Clamp(pressure) >= 70f;
    public static bool ShouldForceCandidate(float pressure) => Clamp(pressure) >= 85f;
}
