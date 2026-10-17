using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public sealed class CropTreatmentFact
{
    private const string OperationPrefix = "crop-treatment:";
    private const float WorkEpsilon = 0.001f;
    private const float PressureEpsilon = 0.0001f;
    private readonly CropTreatmentOrderSaveData snapshot;

    public CropTreatmentFact(
        BuildingInstanceId plotId,
        CropTreatmentOrderSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        bool inputCommitted = source.phase is CropTreatmentOrderPhase.InputCommitted
            or CropTreatmentOrderPhase.OutcomePublished
            or CropTreatmentOrderPhase.LedgerCommitted;
        bool applied = source.phase is CropTreatmentOrderPhase.OutcomePublished
            or CropTreatmentOrderPhase.LedgerCommitted;
        if (!plotId.IsValid
            || source.phase is not (CropTreatmentOrderPhase.Working
                or CropTreatmentOrderPhase.InputCommitted
                or CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.LedgerCommitted)
            || source.outcomeSchemaVersion
                != CropTreatmentOrderSaveData.CurrentOutcomeSchemaVersion
            || source.operationSequence < 0
            || source.operationSequence == int.MaxValue
            || !string.Equals(
                source.operationId,
                FormatOperationId(plotId, source.operationSequence),
                StringComparison.Ordinal)
            || !Enum.IsDefined(typeof(CropTreatmentKind), source.treatmentKind)
            || !IsCanonicalRequired(source.itemId)
            || !IsCanonicalRequired(source.responsibleWorkerId)
            || string.IsNullOrWhiteSpace(source.plotDisplayName)
            || string.IsNullOrWhiteSpace(source.itemDisplayName)
            || string.IsNullOrWhiteSpace(source.responsibleWorkerDisplayName)
            || source.quantity <= 0
            || !float.IsFinite(source.effectAmount)
            || source.effectAmount <= 0f
            || !float.IsFinite(source.requiredWork)
            || source.requiredWork <= 0f
            || !float.IsFinite(source.completedWork)
            || source.completedWork <= 0f
            || source.completedWork > source.requiredWork + WorkEpsilon
            || source.completedWork + WorkEpsilon < source.requiredWork
            || source.scheduledAbsoluteDay < 0
            || source.completedAbsoluteDay < source.scheduledAbsoluteDay
            || source.cooldownDays < 0
            || (long)source.completedAbsoluteDay + source.cooldownDays > int.MaxValue
            || source.cooldownBeforeDay < 0
            || !IsPressure(source.pestPressureBefore)
            || !IsPressure(source.diseasePressureBefore)
            || !IsCanonicalRequired(source.ecologyBeforeFingerprint)
            || source.workContributions == null || source.workContributions.Count == 0
            || source.workContributions.Any(value => value == null
                || !IsCanonicalRequired(value.characterId)
                || !float.IsFinite(value.contributedWork) || value.contributedWork <= 0f)
            || source.workContributions.Select(value => value.characterId).Distinct(StringComparer.Ordinal).Count()
                != source.workContributions.Count
            || source.terminalDisposition != CropTreatmentTerminalDisposition.None
            || !string.IsNullOrEmpty(source.terminalReasonCode)
            || source.terminalLossQuantity != 0 || source.terminalLossMassGrams != 0L
            || !applied && (!string.IsNullOrEmpty(source.ecologyAfterFingerprint)
                || source.pestPressureAfter != 0f || source.diseasePressureAfter != 0f || source.cooldownAfterDay != 0)
            || !inputCommitted && (source.sourceStackIds == null || source.sourceStackIds.Count != 0
                || !string.IsNullOrEmpty(source.commitId) || !string.IsNullOrEmpty(source.requestFingerprint)
                || source.inputMassGrams != 0L || source.tareOutputQuantity != 0
                || source.tareOutputMassGrams != 0L || source.destroyedTareMassGrams != 0L
                || source.tareOutputCommitIds == null || source.tareOutputCommitIds.Count != 0)
            || inputCommitted && !HasExactPhysicalInput(source)
            || applied && !HasExactAppliedResult(source))
        {
            throw new ArgumentException(
                "An exact completed crop-treatment fact is required.",
                nameof(source));
        }

        PlotId = plotId;
        snapshot = source.DeepClone();
        if (snapshot.phase == CropTreatmentOrderPhase.LedgerCommitted)
            snapshot.phase = CropTreatmentOrderPhase.OutcomePublished;
        snapshot.publicationKey = string.Empty;
        snapshot.gameplayOutcomeId = string.Empty;
        snapshot.gameplayOutcomeDigest = string.Empty;
        snapshot.failureReason = string.Empty;
    }

    public BuildingInstanceId PlotId { get; }

    public CropTreatmentOrderSaveData Snapshot() => snapshot.DeepClone();

    private static string FormatOperationId(
        BuildingInstanceId plotId,
        int sequence) => OperationPrefix + plotId.Value + ":"
        + sequence.ToString("D8", CultureInfo.InvariantCulture);

    private static bool HasExactPhysicalInput(CropTreatmentOrderSaveData source)
    {
        IReadOnlyList<string> stackIds = source.sourceStackIds;
        return IsCanonicalRequired(source.commitId)
            && stackIds != null
            && stackIds.Count > 0
            && stackIds.All(IsCanonicalRequired)
            && stackIds.Distinct(StringComparer.Ordinal).Count() == stackIds.Count
            && stackIds.SequenceEqual(
                stackIds.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal)
            && IsCanonicalRequired(source.requestFingerprint)
            && source.inputMassGrams > 0L;
    }

    private static bool HasExactAppliedResult(CropTreatmentOrderSaveData source)
    {
        long expectedCooldown = (long)source.completedAbsoluteDay
            + source.cooldownDays;
        if (expectedCooldown > int.MaxValue
            || source.cooldownAfterDay != expectedCooldown
            || !IsPressure(source.pestPressureAfter)
            || !IsPressure(source.diseasePressureAfter)
            || !IsCanonicalRequired(source.ecologyAfterFingerprint)
            || source.tareOutputQuantity < 0
            || source.tareOutputMassGrams < 0L
            || source.destroyedTareMassGrams < 0L)
        {
            return false;
        }

        float expectedPest = source.treatmentKind is CropTreatmentKind.PestLure
                or CropTreatmentKind.BotanicalPesticide
            ? Math.Max(0f, source.pestPressureBefore - source.effectAmount)
            : source.pestPressureBefore;
        float expectedDisease = source.treatmentKind == CropTreatmentKind.Fungicide
            ? Math.Max(0f, source.diseasePressureBefore - source.effectAmount)
            : source.diseasePressureBefore;
        IReadOnlyList<string> tareCommits = source.tareOutputCommitIds;
        return Math.Abs(source.pestPressureAfter - expectedPest)
                <= PressureEpsilon
            && Math.Abs(source.diseasePressureAfter - expectedDisease)
                <= PressureEpsilon
            && tareCommits != null
            && (source.tareOutputQuantity > 0
                ? source.tareOutputMassGrams > 0L && tareCommits.Count > 0
                : source.tareOutputMassGrams == 0L && tareCommits.Count == 0)
            && tareCommits.All(IsCanonicalRequired)
            && tareCommits.Distinct(StringComparer.Ordinal).Count()
                == tareCommits.Count
            && tareCommits.SequenceEqual(
                tareCommits.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private static bool IsPressure(float value) =>
        float.IsFinite(value) && value is >= 0f and <= 100f;

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
