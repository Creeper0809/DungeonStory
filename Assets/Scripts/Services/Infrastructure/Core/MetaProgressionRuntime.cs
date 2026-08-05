using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
// This concrete adapter owns the legacy scene GUID; persistence contracts live in Meta Core.
public sealed class MetaProgressionRuntime : MonoBehaviour,
    IMetaProgressionPersistencePort,
    IMetaRuntimeEventSink
{
    [SerializeField] private bool showRunResultPanel = true;
    private MetaProgressionState state;
    private IGameClock gameClock;
    private IMetaRunResultBuilder runResultBuilder;
    private IMetaRuntimeApplicationPort applicationPort;
    private MetaRunProgressTracker runProgress;
    private DungeonRuntimeAggregateRootStore aggregateRootStore;

    private MetaRunLifecycleAggregateState Lifecycle => aggregateRootStore.GetOrCreate(() => new MetaRunLifecycleAggregateState());
    private MetaRunLifecycleAggregateState WritableLifecycle => aggregateRootStore.GetOrCreateWritable(
        () => new MetaRunLifecycleAggregateState(),
        current => new MetaRunLifecycleAggregateState { Ended = current?.Ended ?? false, LatestResult = current?.LatestResult });

    public MetaProgressionState State => state ?? throw Missing(nameof(IMetaUpgradeDefinitionCatalog));
    public RunResultSnapshot LatestResult => Lifecycle.LatestResult;
    public MetaRunProgressTracker RunProgress => runProgress ?? throw Missing(nameof(MetaRunProgressTracker));
    public bool HasEnded => Lifecycle.Ended;

    [Inject]
    public void Construct(
        IMetaRunResultBuilder runResultBuilder,
        IMetaRuntimeApplicationPort applicationPort,
        IGameClock gameClock,
        IMetaUpgradeDefinitionCatalog catalog,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        if (this.applicationPort != null && isActiveAndEnabled) this.applicationPort.Unbind(this);
        this.runResultBuilder = runResultBuilder ?? throw new ArgumentNullException(nameof(runResultBuilder));
        this.applicationPort = applicationPort ?? throw new ArgumentNullException(nameof(applicationPort));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.aggregateRootStore = aggregateRootStore ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        state = new MetaProgressionState(catalog ?? throw new ArgumentNullException(nameof(catalog)), aggregateRootStore);
        runProgress = new MetaRunProgressTracker(gameClock, aggregateRootStore);
        StartNewRun();
        if (isActiveAndEnabled) this.applicationPort.Bind(this);
    }

    public void SetShowRunResultPanel(bool value) => showRunResultPanel = value;
    public void StartNewRun() { RunProgress.StartNewRun(gameClock.Time); aggregateRootStore.Replace(new MetaRunLifecycleAggregateState()); }
    public void RestoreRunState(bool ended, RunResultSnapshot result) => aggregateRootStore.Replace(new MetaRunLifecycleAggregateState { Ended = ended, LatestResult = result });
    public MetaProgressionRestoreCandidate PrepareRestore(DungeonMetaProgressionSaveData data) => MetaProgressionRestoreBuilder.Build(data, State.Catalog, gameClock.Time);
    public void Restore(MetaProgressionRestoreCandidate candidate)
    {
        MetaProgressionRestoreCandidate required = candidate ?? throw new ArgumentNullException(nameof(candidate));
        required.CommitTo(aggregateRootStore);
    }

    public bool TryPurchaseUpgrade(string id, out string message)
    {
        bool success = State.TryPurchaseUpgrade(id, out message);
        if (success) applicationPort.PublishUpgradePurchased(new MetaUpgradePurchasedEvent(id), message);
        return success;
    }

    public int GetStartingFacilityCandidateBonus() => MetaProgressionEffects.GetIntegerBonus(State, MetaUpgradeEffectIds.StartingFacilityCandidates);
    public int GetStartingOwnerTraitCandidateBonus() => MetaProgressionEffects.GetIntegerBonus(State, MetaUpgradeEffectIds.StartingOwnerTraitCandidates);
    public float GetOwnerMaxHealthMultiplier() => MetaProgressionEffects.GetMultiplier(State, MetaUpgradeEffectIds.OwnerMaxHealth);
    public float GetInvasionWarningThresholdMultiplier() => MetaProgressionEffects.GetMultiplier(State, MetaUpgradeEffectIds.InvasionWarningThreshold);
    public float GetCommerceStockCostMultiplier(bool eligibleCategory) => eligibleCategory ? MetaProgressionEffects.GetMultiplier(State, MetaUpgradeEffectIds.CommerceStockCost) : 1f;
    public float GetFortressFacilityCostMultiplier(bool defenseFacility) => defenseFacility ? MetaProgressionEffects.GetMultiplier(State, MetaUpgradeEffectIds.FortressDefenseFacilityCost) : 1f;
    public float GetArcaneResearchWorkMultiplier() => MetaProgressionEffects.GetMultiplier(State, MetaUpgradeEffectIds.ArcaneResearchWork);
    public bool IsRecipePreserved(string id) => !string.IsNullOrWhiteSpace(id) && State.PreservedRecipeIds.Contains(id);
    public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<MetaFacilityCandidateSnapshot> candidates)
    {
        int count = MetaProgressionEffects.GetIntegerBonus(State, MetaUpgradeEffectIds.BasicPurchaseEntries);
        return count <= 0 ? Array.Empty<int>() : (candidates ?? Array.Empty<MetaFacilityCandidateSnapshot>())
            .Where(item => item.Eligible).OrderBy(item => item.DefinitionId).Take(count).Select(item => item.DefinitionId).ToArray();
    }

    public void RecordOffenseSuccess() => RunProgress.RecordOffenseSuccess();
    public void RecordOperatingDayStarted(int day) { if (day <= 1 && HasEnded) StartNewRun(); RunProgress.RecordOperatingDayStarted(day); }
    public void RecordOperatingDayReport(int day) => RunProgress.RecordOperatingDayReport(day);
    public void RecordThreat(InvasionThreatStage stage, float threat) => RunProgress.RecordThreat(stage, threat);
    public void RecordInvasionResolved(bool defended) => RunProgress.RecordInvasionResolved(defended);
    public void RecordFacilityDiscovery(int id) => RunProgress.RecordFacilityDiscovery(id);
    public void RecordResearchRecipes(IEnumerable<string> ids) => RunProgress.RecordRecipes(ids);
    public void RecordSynthesis(string recipeId, int resultBuildingId) => RunProgress.RecordSynthesis(recipeId, resultBuildingId);

    public RunResultSnapshot EndRun(string ownerName, string reason, DungeonRunOutcome outcome = DungeonRunOutcome.Defeat)
    {
        if (HasEnded && LatestResult != null) return LatestResult;
        WritableLifecycle.Ended = true;
        MetaRunEnvironmentSnapshot environment = applicationPort.CaptureRunEnvironment();
        RunResultSnapshot result = runResultBuilder.Build(RunProgress.CreateResultContext(ownerName, reason, environment, outcome));
        result = result.WithLegacyCurrency(MetaProgressionCalculator.CalculateLegacyCurrency(result));
        State.AddCurrency(result.legacyCurrency); State.RecordRunCompleted();
        int slots = MetaProgressionEffects.GetIntegerBonus(State, MetaUpgradeEffectIds.PreservedRecipeSlots);
        State.PreserveRecipes(RunProgress.UnlockedRecipeIds.OrderBy(id => id, StringComparer.Ordinal), slots);
        WritableLifecycle.LatestResult = result;
        applicationPort.PublishRunResult(new RunResultReadyEvent(result));
        if (showRunResultPanel) applicationPort.ShowRunResult(result);
        return result;
    }

    private static InvalidOperationException Missing(string dependency) => new InvalidOperationException($"{nameof(MetaProgressionRuntime)} requires {dependency} injection.");
    private void OnEnable() => applicationPort?.Bind(this);
    private void OnDisable() => applicationPort?.Unbind(this);
}
