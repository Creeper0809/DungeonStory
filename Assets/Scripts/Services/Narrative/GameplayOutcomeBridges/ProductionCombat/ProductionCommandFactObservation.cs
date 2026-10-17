using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Narrative.Korean;

public static class ProductionCommandFactObservation
{
    public static ProductionCommandOutcomeReceipt Capture(
        ProductionCommandAppliedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        ProductionCommandStateData before = fact.Before;
        ProductionCommandStateData after = fact.After;
        return new ProductionCommandOutcomeReceipt(
            fact,
            FacilityName(fact),
            Describe(fact.Kind, before),
            Describe(fact.Kind, after));
    }

    private static KoreanNameSnapshot FacilityName(
        ProductionCommandAppliedFact fact)
    {
        string displayRevision =
            "runtime-facility-current-v1:" + fact.FacilityId;
        string pronunciationRevision =
            "runtime-facility-pronunciation-v1:" + fact.FacilityId;
        return new KoreanNameSnapshot(
            fact.FacilityDisplayName,
            displayRevision,
            KoreanPronunciationHint.AutoHangulDisplay(
                pronunciationRevision),
            "ko-KR");
    }

    private static string Describe(
        ProductionCommandKind kind,
        ProductionCommandStateData state) => kind switch
    {
        ProductionCommandKind.BillAdded or
        ProductionCommandKind.BillRemoved => state.exists
            ? DescribeBillState(state)
            : "absent",
        ProductionCommandKind.BillPriorityChanged =>
            $"local-index={state.localIndex}",
        ProductionCommandKind.MinimumCraftQualityChanged =>
            $"tier={state.minimumQuality};blocked={state.blockedCode}",
        ProductionCommandKind.SuspensionChanged =>
            $"suspended={Bool(state.suspended)};reserved-worker={Optional(state.reservedWorkerId)}",
        ProductionCommandKind.StockPolicyChanged or
        ProductionCommandKind.OrderModeChanged => DescribeOrderState(state),
        ProductionCommandKind.DistributionPolicyChanged =>
            DescribeDistributionPolicy(state.distributionMode, state.routes),
        ProductionCommandKind.WorkerPolicyChanged =>
            DescribeWorkerPolicy(state.workerPolicy, state.reservedWorkerId),
        ProductionCommandKind.EmergencyWorkerChanged =>
            "emergency=" + Optional(state.emergencyWorkerId)
            + ";" + DescribeWorkerPolicy(
                state.workerPolicy,
                state.reservedWorkerId),
        ProductionCommandKind.StockSensorInstalled =>
            state.sensorInstalled ? "installed" : "not-installed",
        ProductionCommandKind.StockSensorRemoved =>
            state.sensorInstalled ? "installed" : "removed",
        ProductionCommandKind.StockSensorUnlockAcknowledged =>
            state.sensorAcknowledged ? "acknowledged" : "unacknowledged",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static string DescribeOrderState(
        ProductionCommandStateData state) =>
        $"mode={(int)state.mode};remaining={state.remainingCycles};minimum={state.minimumReserve};target={state.targetStock};pending={Bool(state.hasPendingMode)};pending-mode={(state.hasPendingMode ? (int)state.pendingMode : -1)}";

    private static string DescribeBillState(
        ProductionCommandStateData state) => string.Join(";",
        DescribeOrderState(state),
        "suspended=" + Bool(state.suspended),
        "minimum-quality=" + state.minimumQuality,
        "distribution=" + (int)state.distributionMode,
        "worker-mode=" + (int)(state.workerPolicy?.mode
            ?? WorkerSelectionMode.Anyone),
        "emergency-worker=" + Optional(state.emergencyWorkerId));

    private static string DescribeDistributionPolicy(
        ProductionDistributionMode mode,
        IEnumerable<ProductionConsumerRoutePolicy> routes)
    {
        string values = string.Join(",", (routes
                ?? Array.Empty<ProductionConsumerRoutePolicy>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .Select(value => string.Join("|",
                value.consumerId,
                Bool(value.enabled),
                value.minimumReserve,
                value.targetStock,
                value.priority,
                value.weight,
                value.waitingSeconds.ToString(
                    "R",
                    CultureInfo.InvariantCulture))));
        return $"mode={(int)mode};routes=[{values}]";
    }

    private static string DescribeWorkerPolicy(
        WorkerSelectionPolicySaveData policy,
        string reservedWorkerId)
    {
        WorkerSelectionPolicySaveData value = policy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        return string.Join(";",
            $"mode={(int)value.mode}",
            $"match={(int)value.matchMode}",
            $"sort={(int)value.sortMode}",
            "specific=" + string.Join(",", value.specificCharacterIds),
            "excluded=" + string.Join(",", value.excludedCharacterIds),
            "skill=" + Optional(value.minimumSkillId),
            $"skill-xp={value.minimumSkillExperience}",
            $"career={value.minimumCareerRank}",
            "required-traits=" + string.Join(",", value.requiredTraitIds),
            "excluded-traits=" + string.Join(",", value.excludedTraitIds),
            "reserved-worker=" + Optional(reservedWorkerId));
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Optional(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();
}
