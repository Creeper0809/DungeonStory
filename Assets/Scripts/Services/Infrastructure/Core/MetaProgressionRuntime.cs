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
    private IMetaUpgradePurchaseOutcomeTransaction outcomeTransactions;
    private IMetaRunResultOutcomeTransaction runResultOutcomeTransactions;
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

    [Inject]
    public void ConstructOutcomeTransactions(
        IMetaUpgradePurchaseOutcomeTransaction outcomeTransactions)
    {
        this.outcomeTransactions = outcomeTransactions
            ?? throw new ArgumentNullException(nameof(outcomeTransactions));
    }

    [Inject]
    public void ConstructRunResultOutcomeTransactions(
        IMetaRunResultOutcomeTransaction runResultOutcomeTransactions)
    {
        this.runResultOutcomeTransactions = runResultOutcomeTransactions
            ?? throw new ArgumentNullException(
                nameof(runResultOutcomeTransactions));
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
        MetaUpgradeDefinition definition = State.Catalog.Get(id);
        if (definition == null)
        {
            return State.TryPurchaseUpgrade(id, out message);
        }

        if (outcomeTransactions == null)
        {
            message = "강화 구매 결과 기록이 준비되지 않았습니다.";
            Debug.LogError("meta-upgrade-purchase-outcome-transaction-unavailable");
            return false;
        }

        string upgradeId = definition.id;
        if (!outcomeTransactions.TryReserve(
                upgradeId,
                Math.Max(1, RunProgress.CurrentDay),
                out IMetaUpgradePurchaseOutcomeReservation prepared,
                out string reserveFailure))
        {
            message = "강화 구매 결과를 예약하지 못했습니다.";
            Debug.LogError(
                "meta-upgrade-purchase-outcome-reservation-failed:"
                + reserveFailure);
            return false;
        }

        PurchaseRollbackSnapshot rollback = CapturePurchaseSnapshot();
        int previousLevel = State.GetUpgradeLevel(upgradeId);
        bool success;
        try
        {
            success = State.TryPurchaseUpgrade(upgradeId, out message);
        }
        catch
        {
            CancelPurchaseAndRestore(prepared, rollback);
            throw;
        }

        if (!success)
        {
            CancelPurchaseAndRestore(prepared, rollback);
            return false;
        }

        MetaUpgradePurchaseOutcomeCommitResult committed;
        try
        {
            committed = outcomeTransactions.Commit(
                prepared,
                definition.title,
                previousLevel,
                State.GetUpgradeLevel(upgradeId),
                definition.cost);
        }
        catch
        {
            CancelPurchaseAndRestore(prepared, rollback);
            throw;
        }

        if (!committed.DurablyCommitted)
        {
            RestorePurchaseSnapshot(rollback);
            message = "강화 구매 결과를 확정하지 못했습니다.";
            Debug.LogError(
                "meta-upgrade-purchase-outcome-commit-failed:"
                + committed.DetailCode);
            return false;
        }

        PublishPostCommitUpgradeEvent(upgradeId, message);
        return true;
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
        MetaRunEnvironmentSnapshot environment = applicationPort.CaptureRunEnvironment();
        RunResultSnapshot result = runResultBuilder.Build(RunProgress.CreateResultContext(ownerName, reason, environment, outcome));
        result = result.WithLegacyCurrency(MetaProgressionCalculator.CalculateLegacyCurrency(result));

        if (runResultOutcomeTransactions == null)
            throw Missing(nameof(IMetaRunResultOutcomeTransaction));
        int runSequence = checked(State.CompletedRunCount + 1);
        if (!runResultOutcomeTransactions.TryReserve(
                runSequence,
                Math.Max(1, RunProgress.CurrentDay),
                out IMetaRunResultOutcomeReservation prepared,
                out string reserveFailure))
        {
            throw new InvalidOperationException(
                "Run result outcome reservation failed: " + reserveFailure);
        }

        PurchaseRollbackSnapshot stateBefore = CapturePurchaseSnapshot();
        MetaRunLifecycleAggregateState lifecycleBefore = new()
        {
            Ended = Lifecycle.Ended,
            LatestResult = Lifecycle.LatestResult
        };
        try
        {
            WritableLifecycle.Ended = true;
            State.AddCurrency(result.legacyCurrency);
            State.RecordRunCompleted();
            int slots = MetaProgressionEffects.GetIntegerBonus(
                State,
                MetaUpgradeEffectIds.PreservedRecipeSlots);
            State.PreserveRecipes(
                RunProgress.UnlockedRecipeIds.OrderBy(
                    id => id,
                    StringComparer.Ordinal),
                slots);
            WritableLifecycle.LatestResult = result;

            MetaRunResultOutcomeCommitResult committed =
                runResultOutcomeTransactions.Commit(prepared, result);
            if (!committed.DurablyCommitted)
            {
                RestorePurchaseSnapshot(stateBefore);
                aggregateRootStore.Replace(lifecycleBefore);
                throw new InvalidOperationException(
                    "Run result outcome commit failed: "
                    + committed.DetailCode);
            }
        }
        catch
        {
            runResultOutcomeTransactions.Cancel(prepared);
            RestorePurchaseSnapshot(stateBefore);
            aggregateRootStore.Replace(lifecycleBefore);
            throw;
        }

        PublishPostCommitRunResult(result);
        return result;
    }

    private void PublishPostCommitRunResult(RunResultSnapshot result)
    {
        try
        {
            applicationPort.PublishRunResult(new RunResultReadyEvent(result));
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(
                "meta-run-result-post-commit-observer:"
                + exception.GetType().Name);
        }

        if (!showRunResultPanel)
            return;
        try
        {
            applicationPort.ShowRunResult(result);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(
                "meta-run-result-panel-post-commit-observer:"
                + exception.GetType().Name);
        }
    }

    private void PublishPostCommitUpgradeEvent(string upgradeId, string message)
    {
        try
        {
            applicationPort.PublishUpgradePurchased(
                new MetaUpgradePurchasedEvent(upgradeId),
                message);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(
                "meta-upgrade-purchase-post-commit-observer:"
                + exception.GetType().Name);
        }
    }

    private void CancelPurchaseAndRestore(
        IMetaUpgradePurchaseOutcomeReservation prepared,
        PurchaseRollbackSnapshot rollback)
    {
        try
        {
            outcomeTransactions.Cancel(prepared);
        }
        finally
        {
            RestorePurchaseSnapshot(rollback);
        }
    }

    private PurchaseRollbackSnapshot CapturePurchaseSnapshot() => new(
        State.LifetimeEarnedCurrency,
        State.SpentCurrency,
        State.UpgradeLevels.ToArray(),
        State.PreservedRecipeIds.ToArray(),
        State.CompletedRunCount);

    private void RestorePurchaseSnapshot(PurchaseRollbackSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        State.Restore(
            snapshot.LifetimeEarnedCurrency,
            snapshot.SpentCurrency,
            snapshot.UpgradeLevels,
            snapshot.PreservedRecipeIds,
            snapshot.CompletedRunCount);
    }

    private sealed class PurchaseRollbackSnapshot
    {
        internal PurchaseRollbackSnapshot(
            int lifetimeEarnedCurrency,
            int spentCurrency,
            IReadOnlyList<KeyValuePair<string, int>> upgradeLevels,
            IReadOnlyList<string> preservedRecipeIds,
            int completedRunCount)
        {
            LifetimeEarnedCurrency = lifetimeEarnedCurrency;
            SpentCurrency = spentCurrency;
            UpgradeLevels = upgradeLevels ?? Array.Empty<KeyValuePair<string, int>>();
            PreservedRecipeIds = preservedRecipeIds ?? Array.Empty<string>();
            CompletedRunCount = completedRunCount;
        }

        internal int LifetimeEarnedCurrency { get; }
        internal int SpentCurrency { get; }
        internal IReadOnlyList<KeyValuePair<string, int>> UpgradeLevels { get; }
        internal IReadOnlyList<string> PreservedRecipeIds { get; }
        internal int CompletedRunCount { get; }
    }

    private static InvalidOperationException Missing(string dependency) => new InvalidOperationException($"{nameof(MetaProgressionRuntime)} requires {dependency} injection.");
    private void OnEnable() => applicationPort?.Bind(this);
    private void OnDisable() => applicationPort?.Unbind(this);
}
