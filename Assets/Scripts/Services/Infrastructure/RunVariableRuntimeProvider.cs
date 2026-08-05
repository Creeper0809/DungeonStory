using System;
using System.Collections.Generic;
using VContainer;

public interface IRunVariableRuntimeReader
{
    int GetInitialShopSeed();
    IReadOnlyList<int> GetStartingBlueprintCandidateIds();
    float GetGuestDemandMultiplier(string speciesTag);
    float GetStockCostMultiplier(StockCategory category);
    float GetFacilityShopCostMultiplier(BuildingSO building);
    float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint);
    float GetThreatRiseMultiplier();
    float GetWarningThresholdMultiplier();
    DungeonSurvivalPressure GetSurvivalPressure();
    InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source);
}

public interface IRunSeedProvider
{
    int RunSeed { get; }
}

public interface IOwnerRunDataProvider
{
    CharacterSO SelectedOwnerData { get; }
}

public interface IOwnerRunManagerProvider
{
    bool TryGetManager(out OwnerRunManager manager);
}

public interface IOwnerRunLifecycleService
{
    void HandleOwnerDeath(CharacterActor owner, string reason);
}

public sealed class RunVariableRuntimeReader :
    IRunVariableRuntimeReader,
    IRunSeedProvider,
    ISurvivalPressureProvider
{
    private readonly RunVariableRuntime runtime;
    private readonly IMetaProgressionRuntimeReader metaProgressionReader;

    [Inject]
    public RunVariableRuntimeReader(
        DungeonSceneRuntimeReferences runtimeReferences,
        IMetaProgressionRuntimeReader metaProgressionReader)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .RunVariables
            ?? throw new InvalidOperationException(
                $"{nameof(RunVariableRuntimeReader)} requires a loaded {nameof(RunVariableRuntime)}.");
        this.metaProgressionReader = metaProgressionReader
            ?? throw new ArgumentNullException(nameof(metaProgressionReader));
    }

    public int RunSeed => runtime.RunSeed;

    public int GetInitialShopSeed()
    {
        return runtime.State.StartVariables != null
            ? runtime.State.StartVariables.initialShopSeed
            : 0;
    }

    public IReadOnlyList<int> GetStartingBlueprintCandidateIds()
    {
        return runtime.State.StartVariables != null
            ? runtime.State.StartVariables.startingBlueprintCandidateIds
            : Array.Empty<int>();
    }

    public float GetGuestDemandMultiplier(string speciesTag)
    {
        return runtime.GetGuestDemandMultiplier(speciesTag);
    }

    public float GetStockCostMultiplier(StockCategory category)
    {
        float runMultiplier = runtime.GetStockCostMultiplier(category);
        float metaMultiplier = metaProgressionReader.GetCommerceStockCostMultiplier(category);
        return Math.Max(0.05f, runMultiplier * metaMultiplier);
    }

    public float GetFacilityShopCostMultiplier(BuildingSO building)
    {
        float runMultiplier = runtime.GetFacilityShopCostMultiplier(building);
        float metaMultiplier = metaProgressionReader.GetFortressFacilityCostMultiplier(building);
        return Math.Max(0.05f, runMultiplier * metaMultiplier);
    }

    public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint)
    {
        return runtime.GetBlueprintCostMultiplier(blueprint);
    }

    public float GetThreatRiseMultiplier()
    {
        return runtime.GetThreatRiseMultiplier();
    }

    public float GetWarningThresholdMultiplier()
    {
        return runtime.GetWarningThresholdMultiplier();
    }

    public DungeonSurvivalPressure GetSurvivalPressure()
    {
        return runtime.State.StartVariables != null
            ? runtime.State.StartVariables.survivalPressure
            : DungeonSurvivalPressure.Standard;
    }

    public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source)
    {
        return runtime.ApplyInvasionSettings(source);
    }
}

public sealed class OwnerRunDataProvider : IOwnerRunDataProvider, IOwnerRunManagerProvider
{
    private readonly CharacterSceneRuntimeReferences runtimeReferences;

    public OwnerRunDataProvider(
        CharacterSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public CharacterSO SelectedOwnerData
    {
        get
        {
            return TryGetManager(out OwnerRunManager manager) && manager.selectedOwnerData != null
                ? manager.selectedOwnerData.Value
                : null;
        }
    }

    public bool TryGetManager(out OwnerRunManager manager)
    {
        manager = runtimeReferences.OwnerRunManager;
        return manager != null;
    }
}

public sealed class OwnerRunLifecycleService : IOwnerRunLifecycleService
{
    private readonly IOwnerRunManagerProvider provider;

    public OwnerRunLifecycleService(IOwnerRunManagerProvider provider)
    {
        this.provider = provider
            ?? throw new ArgumentNullException(nameof(provider));
    }

    public void HandleOwnerDeath(CharacterActor owner, string reason)
    {
        if (!provider.TryGetManager(out OwnerRunManager manager))
        {
            throw new InvalidOperationException($"{nameof(OwnerRunLifecycleService)} requires an active {nameof(OwnerRunManager)}.");
        }

        manager.HandleOwnerDeath(owner, reason);
    }
}
