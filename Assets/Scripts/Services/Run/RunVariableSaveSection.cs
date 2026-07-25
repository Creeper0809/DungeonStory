using System;
using System.Collections.Generic;
using System.Linq;

public sealed class RunVariableSaveSection :
    DungeonJsonSaveSection<DungeonRunVariableSaveData>
{
    public const string Id = "run.variables";

    private readonly IRunVariableRuntimeProvider runtimeProvider;

    public RunVariableSaveSection(IRunVariableRuntimeProvider runtimeProvider)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
    }

    public override string SectionId => Id;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Foundation;

    protected override DungeonRunVariableSaveData CapturePayload()
    {
        DungeonRunVariableSaveData destination = new DungeonRunVariableSaveData();
        if (!runtimeProvider.TryGetRuntime(out RunVariableRuntime runtime))
        {
            return destination;
        }

        destination.runSeed = runtime.RunSeed;
        destination.currentDay = runtime.CurrentDay;
        destination.randomDrawMaxima = runtime.RandomDrawMaxima.ToList();
        RunStartVariableSnapshot start = runtime.State.StartVariables;
        destination.hasStartVariables = start != null;
        if (start != null)
        {
            destination.startVariables = new DungeonRunStartSaveData
            {
                seed = start.seed,
                ownerSpeciesTag = start.ownerSpeciesTag,
                ownerDoctrineId = start.ownerDoctrineId,
                difficulty = start.difficulty,
                runDifficulty = start.runDifficulty,
                startingFacilityCandidateIds =
                    start.startingFacilityCandidateIds.ToList(),
                startingGuestSpeciesCandidates =
                    start.startingGuestSpeciesCandidates.ToList(),
                startingBlueprintCandidateIds =
                    start.startingBlueprintCandidateIds.ToList(),
                initialShopSeed = start.initialShopSeed,
                initialDungeonLayoutId = start.initialDungeonLayoutId,
                threatRiseMultiplier = start.threatRiseMultiplier
            };
        }

        destination.activeOperationVariables = runtime.State.ActiveOperationVariables
            .Where(active => active?.Definition != null)
            .Select(active => new DungeonActiveRunVariableSaveData
            {
                definitionId = active.Definition.id,
                startDay = active.StartDay,
                remainingDays = active.RemainingDays
            })
            .ToList();
        destination.invasionVariableId =
            runtime.State.CurrentInvasionVariable?.id ?? string.Empty;
        return destination;
    }

    protected override void RestorePayload(
        DungeonRunVariableSaveData source,
        DungeonGameRestoreReport report)
    {
        if (!runtimeProvider.TryGetRuntime(out RunVariableRuntime runtime))
        {
            report.AddWarning(
                "Run variable runtime was not present; run variables were skipped.");
            return;
        }

        RunStartVariableSnapshot start = null;
        if (source.hasStartVariables)
        {
            DungeonRunStartSaveData savedStart =
                source.startVariables ?? new DungeonRunStartSaveData();
            start = new RunStartVariableSnapshot(
                savedStart.seed,
                savedStart.ownerSpeciesTag,
                savedStart.runDifficulty,
                savedStart.startingFacilityCandidateIds ?? new List<int>(),
                savedStart.startingGuestSpeciesCandidates ?? new List<string>(),
                savedStart.startingBlueprintCandidateIds ?? new List<int>(),
                savedStart.initialShopSeed,
                savedStart.initialDungeonLayoutId,
                savedStart.threatRiseMultiplier,
                !string.IsNullOrWhiteSpace(savedStart.ownerDoctrineId)
                    ? savedStart.ownerDoctrineId
                    : OwnerDoctrineCatalog.ResolveForSpecies(
                        savedStart.ownerSpeciesTag)?.id);
        }

        List<ActiveRunVariable> activeVariables = new List<ActiveRunVariable>();
        foreach (DungeonActiveRunVariableSaveData saved in
                 source.activeOperationVariables
                 ?? new List<DungeonActiveRunVariableSaveData>())
        {
            RunVariableDefinition definition =
                RunVariableCatalog.Get(saved?.definitionId);
            if (definition == null)
            {
                report.AddWarning(
                    $"Run variable '{saved?.definitionId}' no longer exists.");
                continue;
            }

            activeVariables.Add(new ActiveRunVariable(
                definition,
                saved.startDay,
                saved.remainingDays));
        }

        runtime.RestoreRun(
            source.runSeed,
            source.currentDay,
            start,
            activeVariables,
            RunVariableCatalog.Get(source.invasionVariableId),
            source.randomDrawMaxima);
    }
}
