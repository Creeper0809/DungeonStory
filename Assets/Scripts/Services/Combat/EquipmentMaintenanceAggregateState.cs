using System;
using System.Collections.Generic;

internal sealed class EquipmentMaintenanceAggregateState
{
    internal Dictionary<string, EquipmentMaintenancePolicyData> Policies { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<string, string> Assignments { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<string, CombatEquipmentRepairOrder> Orders { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<string, CombatEquipmentRepairTerminalEffectSaveData>
        TerminalEffects { get; } = new(StringComparer.Ordinal);
    internal int PolicySequence { get; set; }
    internal int OrderSequence { get; set; }

    internal EquipmentMaintenanceAggregateState Clone()
    {
        EquipmentMaintenanceAggregateState clone = new()
        {
            PolicySequence = PolicySequence,
            OrderSequence = OrderSequence
        };
        foreach (KeyValuePair<string, EquipmentMaintenancePolicyData> pair in Policies)
        {
            clone.Policies.Add(pair.Key, pair.Value.Clone());
        }
        foreach (KeyValuePair<string, string> pair in Assignments)
        {
            clone.Assignments.Add(pair.Key, pair.Value);
        }
        foreach (KeyValuePair<string, CombatEquipmentRepairOrder> pair in Orders)
        {
            clone.Orders.Add(pair.Key, pair.Value.Clone());
        }
        foreach (KeyValuePair<string,
                     CombatEquipmentRepairTerminalEffectSaveData> pair in
                 TerminalEffects)
        {
            clone.TerminalEffects.Add(pair.Key, pair.Value.Clone());
        }

        return clone;
    }

    internal static EquipmentMaintenanceAggregateState CreateDefault()
    {
        EquipmentMaintenanceAggregateState state = new();
        state.Policies.Add(
            EquipmentMaintenancePolicyRuntime.StandardPolicyId,
            new EquipmentMaintenancePolicyData
            {
                id = EquipmentMaintenancePolicyRuntime.StandardPolicyId,
                displayName = "표준",
                automaticRepair = true,
                sendAtDurability = 0.35f,
                returnAtDurability = 0.9f,
                preferReplacement = true
            });
        state.Policies.Add(
            EquipmentMaintenancePolicyRuntime.PreventivePolicyId,
            new EquipmentMaintenancePolicyData
            {
                id = EquipmentMaintenancePolicyRuntime.PreventivePolicyId,
                displayName = "예방 정비",
                automaticRepair = true,
                sendAtDurability = 0.6f,
                returnAtDurability = 1f,
                preferReplacement = true
            });
        state.Policies.Add(
            EquipmentMaintenancePolicyRuntime.ManualPolicyId,
            new EquipmentMaintenancePolicyData
            {
                id = EquipmentMaintenancePolicyRuntime.ManualPolicyId,
                displayName = "수동",
                automaticRepair = false,
                sendAtDurability = 0f,
                returnAtDurability = 1f
            });
        return state;
    }
}

public sealed class EquipmentMaintenanceRestoreCandidate
{
    internal EquipmentMaintenanceRestoreCandidate(
        EquipmentMaintenanceAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal EquipmentMaintenanceAggregateState State { get; }
}
