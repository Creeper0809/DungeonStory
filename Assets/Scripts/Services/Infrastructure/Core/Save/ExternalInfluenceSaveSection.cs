using System;
using System.Collections.Generic;
using System.Linq;
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

    protected override void NormalizeRestorePayload(
        DungeonExternalInfluenceSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload?.dreadAffectedIntruderIds == null)
        {
            return;
        }

        bool changed = false;
        for (int index = 0;
             index < payload.dreadAffectedIntruderIds.Count;
             index++)
        {
            string previous = payload.dreadAffectedIntruderIds[index];
            payload.dreadAffectedIntruderIds[index] =
                NormalizeV18CharacterReference(
                    previous,
                    report,
                    $"dreadAffectedIntruderIds[{index}]");
            changed |= !string.Equals(
                previous,
                payload.dreadAffectedIntruderIds[index],
                StringComparison.Ordinal);
        }
        if (changed)
        {
            payload.dreadAffectedIntruderIds.Sort(StringComparer.Ordinal);
        }
    }

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
            || payload.dreadAffectedIntruderIds == null
            || payload.operations == null)
        {
            report.AddError(
                "External-influence payload, ID collection, or operation history is null.");
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
                && payload.ecologyRaidSequence <= 0
            || (payload.ecologyRaidScheduled
                    || payload.ecologyRaidInProgress)
                && !payload.ecologyWarningIssued)
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
        ValidateOperations(payload, report);

        bool trailCharmValid = payload.trailCharmCommitPhase
            == ExternalInfluenceTrailCharmCommitPhase.None
                ? ExternalInfluenceTrailCharmSaveContract.HasEmptyProvenance(payload)
                : ExternalInfluenceTrailCharmSaveContract.IsStructurallyValid(payload);
        bool pendingSitePublished = payload.intelUnlockedSiteIds.Contains(
            payload.pendingTrailCharmSiteId,
            StringComparer.Ordinal);
        if (!trailCharmValid
            || payload.trailCharmCommitPhase
                    == ExternalInfluenceTrailCharmCommitPhase.ItemCommitted
                && pendingSitePublished
            || payload.trailCharmCommitPhase
                    == ExternalInfluenceTrailCharmCommitPhase.IntelPublished
                && !pendingSitePublished)
        {
            report.AddError(
                "External-influence trail-charm pending receipt is invalid.");
        }
    }

    private static void ValidateOperations(
        DungeonExternalInfluenceSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload.nextOperationRevision <= 0L)
        {
            report.AddError(
                "External-influence next operation revision is invalid.");
            return;
        }

        long previousRevision = 0L;
        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < payload.operations.Count; index++)
        {
            ExternalInfluenceOperationSaveData operation =
                payload.operations[index];
            if (operation == null
                || operation.schemaVersion
                    != ExternalInfluenceOperationSaveData.CurrentSchemaVersion
                || operation.revision <= previousRevision
                || operation.revision >= payload.nextOperationRevision
                || !IsCanonicalRequired(operation.operationId)
                || !operationIds.Add(operation.operationId)
                || !Enum.IsDefined(
                    typeof(ExternalInfluenceOperationKind),
                    operation.kind)
                || !Enum.IsDefined(
                    typeof(ExternalInfluenceOperationPhase),
                    operation.phase)
                || !Enum.IsDefined(
                    typeof(ExternalInfluenceCostKind),
                    operation.costKind)
                || !IsCanonicalRequired(operation.sourceId)
                || !IsCanonicalOptional(operation.targetId)
                || !IsCanonicalOptional(operation.financeTransactionId)
                || !IsCanonicalOptional(operation.physicalCommitId)
                || !IsCanonicalOptional(operation.failureReason)
                || !IsFinite(operation.costAmount)
                || operation.costAmount < 0f
                || !IsFinite(operation.requestedAmount)
                || !IsFinite(operation.appliedAmount)
                || !IsFinite(operation.valueBefore)
                || !IsFinite(operation.valueAfter)
                || operation.newlyRevealed < 0
                || operation.resultCount < 0
                || operation.recordedDay < -1)
            {
                report.AddError(
                    $"External-influence operation at index {index} is invalid.");
                return;
            }

            bool requiresTarget = operation.kind is
                ExternalInfluenceOperationKind.ReconUpgrade
                or ExternalInfluenceOperationKind.RumorMitigation
                or ExternalInfluenceOperationKind.IntelUnlock
                or ExternalInfluenceOperationKind.DreadDefense
                or ExternalInfluenceOperationKind.EcologyRaid;
            if (requiresTarget && operation.targetId.Length == 0
                || operation.costKind == ExternalInfluenceCostKind.Gold
                    && operation.financeTransactionId.Length == 0
                || operation.costKind == ExternalInfluenceCostKind.TrailCharm
                    && operation.physicalCommitId.Length == 0
                || operation.phase == ExternalInfluenceOperationPhase.Failed
                    && operation.failureReason.Length == 0)
            {
                report.AddError(
                    $"External-influence operation '{operation.operationId}' lacks required provenance.");
                return;
            }

            previousRevision = operation.revision;
        }

        if (payload.operations.Count > 0
            && payload.nextOperationRevision <= previousRevision)
        {
            report.AddError(
                "External-influence next operation revision does not advance history.");
        }

        if (payload.trailCharmCommitPhase
                != ExternalInfluenceTrailCharmCommitPhase.None)
        {
            string operationId =
                $"external-influence:intel:{payload.pendingTrailCharmSiteId}";
            ExternalInfluenceOperationSaveData pending = payload.operations
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.operationId,
                        operationId,
                        StringComparison.Ordinal));
            if (pending == null
                || pending.kind != ExternalInfluenceOperationKind.IntelUnlock
                || pending.costKind != ExternalInfluenceCostKind.TrailCharm
                || !string.Equals(
                    pending.physicalCommitId,
                    payload.pendingTrailCharmCommitId,
                    StringComparison.Ordinal)
                || pending.phase is not (
                    ExternalInfluenceOperationPhase.CostCommitted
                    or ExternalInfluenceOperationPhase.ResultPublished))
            {
                report.AddError(
                    "External-influence pending trail-charm operation provenance is missing.");
            }
        }
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

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalOptional(string value) =>
        value != null
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool InRange(float value, float minimum, float maximum) =>
        IsFiniteNonNegative(value) && value >= minimum && value <= maximum;
}
