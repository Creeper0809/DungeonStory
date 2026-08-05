using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public interface IRunVariableRestorePublisher
{
    void PublishRestoreState(RunVariableAggregateState candidate);
}

public sealed class RunVariableSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonRunVariableSaveData,
        RunVariableAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = DungeonSaveSectionIds.RunVariables;

    private readonly IRunVariableRuntime runtime;
    private readonly IRunVariableRestorePublisher restorePublisher;
    private readonly IRunVariableDefinitionCatalog variableCatalog;
    private readonly IOwnerDoctrineDefinitionCatalog doctrineCatalog;

    public RunVariableSaveSection(
        IRunVariableRuntime runtime,
        IRunVariableDefinitionCatalog variableCatalog,
        IOwnerDoctrineDefinitionCatalog doctrineCatalog)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
        restorePublisher = runtime as IRunVariableRestorePublisher
            ?? throw new InvalidOperationException(
                $"{nameof(RunVariableSaveSection)} requires a detached-state restore publisher.");
        this.variableCatalog = variableCatalog
            ?? throw new ArgumentNullException(nameof(variableCatalog));
        this.doctrineCatalog = doctrineCatalog
            ?? throw new ArgumentNullException(nameof(doctrineCatalog));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonRunVariableSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Foundation;

    protected override DungeonRunVariableSaveData CapturePayload() =>
        runtime.CaptureForSave();

    protected override RunVariableAggregateState BuildRestoreCandidate(
        DungeonRunVariableSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ValidatePayload(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Run-variable restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        RunStartVariableSnapshot start = null;
        if (payload.hasStartVariables)
        {
            DungeonRunStartSaveData saved = payload.startVariables;
            start = new RunStartVariableSnapshot(
                saved.seed,
                saved.ownerSpeciesTag,
                saved.runDifficulty,
                saved.startingFacilityCandidateIds,
                saved.startingGuestSpeciesCandidates,
                saved.startingBlueprintCandidateIds,
                saved.initialShopSeed,
                saved.initialDungeonLayoutId,
                saved.threatRiseMultiplier,
                saved.ownerDoctrineId,
                saved.survivalPressure);
        }

        List<ActiveRunVariable> operations = payload.activeOperationVariables
            .Select(saved => new ActiveRunVariable(
                variableCatalog.Require(saved.definitionId),
                saved.startDay,
                saved.remainingDays))
            .ToList();
        RunVariableDefinition invasion =
            payload.invasionVariableId.Length == 0
                ? null
                : variableCatalog.Require(payload.invasionVariableId);
        RunVariableAggregateState candidate = new(
            payload.runSeed,
            payload.currentDay);
        candidate.Variables.Restore(start, operations, invasion);
        return candidate;
    }

    protected override void PublishRestoreCandidate(
        RunVariableAggregateState candidate) =>
        restorePublisher.PublishRestoreState(candidate);

    private void ValidatePayload(
        DungeonRunVariableSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload == null || payload.activeOperationVariables == null)
        {
            report.AddError(
                "Run-variable payload or operation list is null.");
            return;
        }
        if (payload.version != DungeonRunVariableSaveData.CurrentVersion)
        {
            report.AddError(
                $"Run-variable payload version {payload.version} is unsupported.");
        }
        if (payload.runSeed == 0 || payload.currentDay < 1)
        {
            report.AddError("Run-variable seed or current day is invalid.");
        }

        if (payload.hasStartVariables)
        {
            ValidateStart(payload, report);
        }
        else if (!IsCanonicalAbsentStart(payload.startVariables)
                 || payload.activeOperationVariables.Count != 0
                 || !string.IsNullOrEmpty(payload.invasionVariableId))
        {
            report.AddError(
                "Run-variable unstarted state contains active run data.");
        }

        HashSet<string> activeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonActiveRunVariableSaveData saved in
                 payload.activeOperationVariables)
        {
            string definitionId = saved?.definitionId;
            RunVariableDefinition definition = variableCatalog.Get(definitionId);
            if (saved == null
                || !IsCanonicalRequired(definitionId)
                || definition == null
                || definition.category != RunVariableCategory.Operation
                || !activeIds.Add(definitionId)
                || saved.startDay < 1
                || saved.startDay > payload.currentDay
                || saved.remainingDays < 1
                || saved.remainingDays > definition.activeDays)
            {
                report.AddError(
                    "Run-variable payload contains an invalid operation variable.");
            }
        }

        if (!IsCanonicalOptional(payload.invasionVariableId))
        {
            report.AddError(
                "Run-variable invasion definition ID is non-canonical.");
        }
        else if (payload.invasionVariableId.Length > 0)
        {
            RunVariableDefinition invasion =
                variableCatalog.Get(payload.invasionVariableId);
            if (invasion == null
                || invasion.category != RunVariableCategory.Invasion)
            {
                report.AddError(
                    "Run-variable invasion definition is missing or has the wrong category.");
            }
        }
    }

    private void ValidateStart(
        DungeonRunVariableSaveData payload,
        DungeonGameRestoreReport report)
    {
        DungeonRunStartSaveData start = payload.startVariables;
        if (start == null
            || start.startingFacilityCandidateIds == null
            || start.startingGuestSpeciesCandidates == null
            || start.startingBlueprintCandidateIds == null)
        {
            report.AddError(
                "Run-variable start state or candidate list is null.");
            return;
        }
        if (start.seed != payload.runSeed
            || !IsCanonicalRequired(start.ownerSpeciesTag)
            || !IsCanonicalRequired(start.ownerDoctrineId)
            || doctrineCatalog.Get(start.ownerDoctrineId) == null
            || !Enum.IsDefined(typeof(DungeonDifficulty), start.runDifficulty)
            || !Enum.IsDefined(
                typeof(DungeonSurvivalPressure),
                start.survivalPressure)
            || !IsCanonicalOptional(start.initialDungeonLayoutId)
            || float.IsNaN(start.threatRiseMultiplier)
            || float.IsInfinity(start.threatRiseMultiplier)
            || start.threatRiseMultiplier < 0.05f)
        {
            report.AddError(
                "Run-variable start state contains invalid scalar or authored data.");
        }
        if (!HasUniquePositiveIds(start.startingFacilityCandidateIds)
            || !HasUniquePositiveIds(start.startingBlueprintCandidateIds)
            || !HasUniqueCanonicalStrings(
                start.startingGuestSpeciesCandidates))
        {
            report.AddError(
                "Run-variable start candidate lists are invalid.");
        }
    }

    private static bool HasUniquePositiveIds(IReadOnlyList<int> values)
    {
        HashSet<int> unique = new HashSet<int>();
        return values.All(value => value > 0 && unique.Add(value));
    }

    private static bool IsCanonicalAbsentStart(DungeonRunStartSaveData start)
    {
        // JsonUtility materializes a null nested serializable class as an object
        // populated with its field initializers. Treat only that exact shape as
        // the serialized representation of an absent start snapshot.
        return start == null
            || (start.seed == 0
                && string.IsNullOrEmpty(start.ownerSpeciesTag)
                && string.IsNullOrEmpty(start.ownerDoctrineId)
                && start.runDifficulty == DungeonDifficulty.Normal
                && start.survivalPressure == DungeonSurvivalPressure.Standard
                && start.startingFacilityCandidateIds != null
                && start.startingFacilityCandidateIds.Count == 0
                && start.startingGuestSpeciesCandidates != null
                && start.startingGuestSpeciesCandidates.Count == 0
                && start.startingBlueprintCandidateIds != null
                && start.startingBlueprintCandidateIds.Count == 0
                && start.initialShopSeed == 0
                && string.IsNullOrEmpty(start.initialDungeonLayoutId)
                && Math.Abs(start.threatRiseMultiplier - 1f) < 0.0001f);
    }

    private static bool HasUniqueCanonicalStrings(IReadOnlyList<string> values)
    {
        HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
        return values.All(value =>
            IsCanonicalRequired(value) && unique.Add(value));
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalOptional(string value) =>
        value != null
        && (value.Length == 0 || IsCanonicalRequired(value));
}
