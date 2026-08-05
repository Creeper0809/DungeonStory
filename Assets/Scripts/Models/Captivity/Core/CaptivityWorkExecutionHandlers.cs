using UnityEngine;

public static class CaptivityWorkExecutionRules
{
    public static float GetWardenUrgency(CaptiveState state) =>
        state == null ? 0f : Mathf.Clamp01(0.45f + state.escapeRisk * 0.005f);

    public static bool IsWardenCompleted(CaptiveState state) =>
        state?.status == CaptivityStatus.Confined;

    public static float GetPerformUrgency(bool hasOrder) => hasOrder ? 0.7f : 0f;

    public static bool IsPerformancePreparationCompleted(CircusShowOrder order) =>
        order != null
        && order.state != CircusShowState.Composition
        && order.state != CircusShowState.Cancelled;
}
