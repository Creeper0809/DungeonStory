using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class AutomationPowerDemandRegistry
{
    private readonly Dictionary<string, AutomationMode> modes =
        new Dictionary<string, AutomationMode>(StringComparer.Ordinal);

    public int Version { get; private set; }

    public AutomationMode GetMode(string facilityId)
    {
        return !string.IsNullOrWhiteSpace(facilityId)
            && modes.TryGetValue(facilityId, out AutomationMode mode)
                ? mode
                : AutomationMode.Manual;
    }

    public float ResolveDemand(
        string facilityId,
        BuildingAutomationAbility ability)
    {
        return AutomationPowerDemandRules.Resolve(
            GetMode(facilityId),
            ability);
    }

    public void SetMode(string facilityId, AutomationMode mode)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return;
        }

        string normalizedId = facilityId.Trim();
        if (modes.TryGetValue(normalizedId, out AutomationMode current)
            && current == mode)
        {
            return;
        }

        modes[normalizedId] = mode;
        Touch();
    }

    public void Clear()
    {
        if (modes.Count == 0)
        {
            return;
        }

        modes.Clear();
        Touch();
    }

    private void Touch()
    {
        unchecked
        {
            Version++;
        }
    }
}

public static class AutomationPowerDemandRules
{
    public static float Resolve(
        AutomationMode mode,
        BuildingAutomationAbility ability)
    {
        if (ability == null)
        {
            return 0f;
        }

        return mode switch
        {
            AutomationMode.PoweredAssist =>
                Mathf.Max(0f, ability.assistedPowerDemand),
            AutomationMode.Automatic =>
                Mathf.Max(0f, ability.automaticPowerDemand),
            _ => 0f
        };
    }
}
