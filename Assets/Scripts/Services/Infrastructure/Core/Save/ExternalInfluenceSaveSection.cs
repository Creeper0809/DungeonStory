using System;
using System.Collections.Generic;
using DungeonStory.Foundation;

public sealed class ExternalInfluenceSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonExternalInfluenceSaveData,
        ExternalInfluenceRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "external.influence";
    private static readonly string[] Dependencies =
    {
        DungeonSaveSectionIds.PhysicalItems,
        DungeonSaveSectionIds.WildlifePopulation
    };

    private readonly IExternalInfluenceRuntime runtime;

    public ExternalInfluenceSaveSection(
        IExternalInfluenceRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonExternalInfluenceSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonExternalInfluenceSaveData CapturePayload() =>
        runtime.Capture();

    protected override ExternalInfluenceRestoreCandidate BuildRestoreCandidate(
        DungeonExternalInfluenceSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ExternalInfluenceSaveValidation.Validate(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "External-influence restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        return runtime.BuildRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        ExternalInfluenceRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);
}

public static class ExternalInfluenceSaveValidation
{
    public static void Validate(
        DungeonExternalInfluenceSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload == null
            || payload.intelUnlockedSiteIds == null
            || payload.dreadAffectedIntruderIds == null)
        {
            report.AddError(
                "External-influence payload or ID collection is null.");
            return;
        }
        if (payload.version != DungeonExternalInfluenceSaveData.CurrentVersion)
        {
            report.AddError(
                $"External-influence payload version {payload.version} is unsupported.");
        }
        bool namedStateValid =
            DungeonStory.Environment.ExternalInfluenceRules.IsValid(
                new DungeonStory.Environment.ExternalInfluenceSnapshot(
                    payload.renown,
                    payload.dread,
                    payload.hostileRumor,
                    payload.ecologyPressure,
                    payload.scoutingLabor,
                    payload.currentOperatingDay,
                    payload.lastRumorMitigationDay,
                    payload.dreadDefenseArmed,
                    payload.dreadDefenseActive,
                    payload.dreadDefenseBoss));
        if (!namedStateValid
            || !InRange(payload.lastExposedFoodPressure, 0f, 20f)
            || !IsFiniteNonNegative(payload.lastWeatherPressure)
            || !IsFiniteNonNegative(payload.ecologyRaidRemainingSeconds))
        {
            report.AddError(
                "External-influence payload contains invalid numeric pressure state.");
        }
        if (payload.currentOperatingDay < -1
            || payload.lastRumorMitigationDay < -1
            || payload.lastRumorMitigationDay > payload.currentOperatingDay
            || payload.ecologyRaidSequence < 0)
        {
            report.AddError(
                "External-influence payload contains invalid day or sequence state.");
        }
        if (payload.dreadDefenseArmed && payload.dreadDefenseActive
            || !payload.dreadDefenseActive
                && (payload.dreadDefenseBoss
                    || payload.dreadAffectedIntruderIds.Count != 0))
        {
            report.AddError(
                "External-influence dread-defense hierarchy is invalid.");
        }

        int ecologyStateCount = (payload.ecologyRaidScheduled ? 1 : 0)
            + (payload.ecologyRaidInProgress ? 1 : 0)
            + (payload.ecologyResolutionReported ? 1 : 0);
        if (ecologyStateCount > 1
            || payload.ecologyRaidScheduled
                && payload.ecologyRaidRemainingSeconds <= 0f
            || !payload.ecologyRaidScheduled
                && payload.ecologyRaidRemainingSeconds != 0f
            || (payload.ecologyRaidScheduled
                    || payload.ecologyRaidInProgress
                    || payload.ecologyResolutionReported)
                && (payload.ecologyRaidSequence <= 0
                    || !payload.ecologyWarningIssued))
        {
            report.AddError(
                "External-influence ecology-raid hierarchy is invalid.");
        }

        ValidateOrderedIds(
            payload.intelUnlockedSiteIds,
            "intel site",
            report);
        ValidateOrderedIds(
            payload.dreadAffectedIntruderIds,
            "dread intruder",
            report);
    }

    private static void ValidateOrderedIds(
        IReadOnlyList<string> values,
        string kind,
        DungeonGameRestoreReport report)
    {
        string previous = null;
        for (int index = 0; index < values.Count; index++)
        {
            string value = values[index];
            if (string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
                || previous != null
                    && string.CompareOrdinal(previous, value) >= 0)
            {
                report.AddError(
                    $"External-influence {kind} IDs are non-canonical or unordered.");
                return;
            }
            previous = value;
        }
    }

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool InRange(float value, float minimum, float maximum) =>
        IsFiniteNonNegative(value) && value >= minimum && value <= maximum;
}
