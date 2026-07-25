using System;
using System.Collections.Generic;
using VContainer;

public interface IRunVariableRuntimeProvider
{
    bool TryGetRuntime(out RunVariableRuntime runtime);
}

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
    InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source);
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

public sealed class RunVariableRuntimeProvider :
    IRunVariableRuntimeProvider
{
    private readonly DungeonSceneRuntimeReferences runtimeReferences;

    public RunVariableRuntimeProvider(
        DungeonSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool TryGetRuntime(out RunVariableRuntime resolvedRuntime)
    {
        resolvedRuntime = runtimeReferences.RunVariables;
        return resolvedRuntime != null;
    }
}

public sealed class RunVariableRuntimeReader : IRunVariableRuntimeReader
{
    private readonly IRunVariableRuntimeProvider provider;
    private readonly IMetaProgressionRuntimeReader metaProgressionReader;

    public RunVariableRuntimeReader(IRunVariableRuntimeProvider provider)
        : this(provider, null)
    {
    }

    [Inject]
    public RunVariableRuntimeReader(
        IRunVariableRuntimeProvider provider,
        IMetaProgressionRuntimeReader metaProgressionReader)
    {
        this.provider = provider
            ?? throw new ArgumentNullException(nameof(provider));
        this.metaProgressionReader = metaProgressionReader;
    }

    public int GetInitialShopSeed()
    {
        return provider.TryGetRuntime(out RunVariableRuntime runtime)
            && runtime.State.StartVariables != null
            ? runtime.State.StartVariables.initialShopSeed
            : 0;
    }

    public IReadOnlyList<int> GetStartingBlueprintCandidateIds()
    {
        return provider.TryGetRuntime(out RunVariableRuntime runtime)
            && runtime.State.StartVariables != null
            ? runtime.State.StartVariables.startingBlueprintCandidateIds
            : Array.Empty<int>();
    }

    public float GetGuestDemandMultiplier(string speciesTag)
    {
        return provider.TryGetRuntime(out RunVariableRuntime runtime)
            ? runtime.GetGuestDemandMultiplier(speciesTag)
            : 1f;
    }

    public float GetStockCostMultiplier(StockCategory category)
    {
        float runMultiplier = provider.TryGetRuntime(out RunVariableRuntime runtime)
            ? runtime.GetStockCostMultiplier(category)
            : 1f;
        float metaMultiplier = metaProgressionReader?.GetCommerceStockCostMultiplier(category) ?? 1f;
        return Math.Max(0.05f, runMultiplier * metaMultiplier);
    }

    public float GetFacilityShopCostMultiplier(BuildingSO building)
    {
        float runMultiplier = provider.TryGetRuntime(out RunVariableRuntime runtime)
            ? runtime.GetFacilityShopCostMultiplier(building)
            : 1f;
        float metaMultiplier = metaProgressionReader?.GetFortressFacilityCostMultiplier(building) ?? 1f;
        return Math.Max(0.05f, runMultiplier * metaMultiplier);
    }

    public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint)
    {
        return provider.TryGetRuntime(out RunVariableRuntime runtime)
            ? runtime.GetBlueprintCostMultiplier(blueprint)
            : 1f;
    }

    public float GetThreatRiseMultiplier()
    {
        return provider.TryGetRuntime(out RunVariableRuntime runtime)
            ? runtime.GetThreatRiseMultiplier()
            : 1f;
    }

    public float GetWarningThresholdMultiplier()
    {
        return provider.TryGetRuntime(out RunVariableRuntime runtime)
            ? runtime.GetWarningThresholdMultiplier()
            : 1f;
    }

    public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source)
    {
        return provider.TryGetRuntime(out RunVariableRuntime runtime)
            ? runtime.ApplyInvasionSettings(source)
            : source;
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
