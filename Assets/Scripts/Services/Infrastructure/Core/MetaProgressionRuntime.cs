using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
// This concrete adapter owns the legacy scene GUID; persistence contracts live in Meta Core.
public sealed class MetaProgressionRuntime : MonoBehaviour,
    IMetaProgressionPersistencePort,
    IRunCompletionPreparation,
    IMetaRuntimeEventSink
{
    [SerializeField] private bool showRunResultPanel = true;
    private MetaProgressionState state;
    private IGameClock gameClock;
    private IMetaRunResultBuilder runResultBuilder;
    private IMetaRuntimeApplicationPort applicationPort;
    private IDomainFactPublisher domainFacts;
    private MetaRunProgressTracker runProgress;
    private DungeonRuntimeAggregateRootStore aggregateRootStore;

    private MetaRunLifecycleAggregateState Lifecycle => aggregateRootStore.GetOrCreate(() => new MetaRunLifecycleAggregateState());
    private MetaRunLifecycleAggregateState WritableLifecycle => aggregateRootStore.GetOrCreateWritable(
        () => new MetaRunLifecycleAggregateState(),
        current => new MetaRunLifecycleAggregateState { Ended = current?.Ended ?? false,
            LatestResult = current?.LatestResult, PendingCompletion = current?.PendingCompletion });

    private MetaProgressionState OwnedState => state ?? throw Missing(nameof(IMetaUpgradeDefinitionCatalog));
    public RunResultSnapshot LatestResult => Lifecycle.LatestResult;
    private MetaRunProgressTracker OwnedRunProgress => runProgress ?? throw Missing(nameof(MetaRunProgressTracker));
    public MetaProgressionState State => OwnedState.CreateSnapshot();
    public MetaRunProgressTracker RunProgress => OwnedRunProgress.CreateSnapshot();
#if UNITY_EDITOR
    public void AddCurrencyForDebug(int amount) => OwnedState.AddCurrency(amount);
    public void SetUpgradeLevelForDebug(string id, int level) => OwnedState.SetUpgradeLevelForDebug(id, level);
#endif
    public bool HasEnded => Lifecycle.Ended;
    public MetaRunCompletionSnapshot PendingCompletion => Lifecycle.PendingCompletion;

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
    public void ConstructDomainFacts(IDomainFactPublisher domainFacts)
    {
        this.domainFacts = domainFacts
            ?? throw new ArgumentNullException(nameof(domainFacts));
    }

    public void SetShowRunResultPanel(bool value) => showRunResultPanel = value;
    public void StartNewRun() { OwnedRunProgress.StartNewRun(gameClock.Time); aggregateRootStore.Replace(new MetaRunLifecycleAggregateState()); }
    public void RestoreRunState(bool ended, RunResultSnapshot result) => aggregateRootStore.Replace(new MetaRunLifecycleAggregateState { Ended = ended, LatestResult = result });
    public MetaProgressionRestoreCandidate PrepareRestore(DungeonMetaProgressionSaveData data) => MetaProgressionRestoreBuilder.Build(data, OwnedState.Catalog, gameClock.Time);
    public void Restore(MetaProgressionRestoreCandidate candidate)
    {
        MetaProgressionRestoreCandidate required = candidate ?? throw new ArgumentNullException(nameof(candidate));
        required.CommitTo(aggregateRootStore);
    }

    public bool TryPurchaseUpgrade(string id, out string message)
    {
        MetaUpgradeDefinition definition = OwnedState.Catalog.Get(id);
        if (definition == null)
        {
            return OwnedState.TryPurchaseUpgrade(id, out message);
        }

        if (domainFacts == null)
        {
            message = "강화 구매 결과 기록이 준비되지 않았습니다.";
            Debug.LogError("meta-upgrade-purchase-domain-fact-publisher-unavailable");
            return false;
        }

        string upgradeId = definition.id;
        int previousLevel = OwnedState.GetUpgradeLevel(upgradeId);
        int absoluteDay = Math.Max(1, OwnedRunProgress.CurrentDay);
        var proposedFact = new MetaUpgradePurchaseDomainFact(
            upgradeId,
            definition.title,
            previousLevel,
            previousLevel + 1,
            definition.cost,
            absoluteDay);
        if (!domainFacts.TryReserve(
                proposedFact,
                out DomainFactLease prepared,
                out string reserveFailure))
        {
            message = "강화 구매 결과를 예약하지 못했습니다.";
            Debug.LogError(
                "meta-upgrade-purchase-outcome-reservation-failed:"
                + reserveFailure);
            return false;
        }

        PurchaseRollbackSnapshot rollback = CapturePurchaseSnapshot();
        bool success;
        try
        {
            success = OwnedState.TryPurchaseUpgrade(upgradeId, out message);
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

        DomainFactDelivery committed;
        try
        {
            committed = domainFacts.Publish(
                prepared,
                new MetaUpgradePurchaseDomainFact(
                    upgradeId,
                    definition.title,
                    previousLevel,
                    OwnedState.GetUpgradeLevel(upgradeId),
                    definition.cost,
                    absoluteDay));
        }
        catch
        {
            CancelPurchaseAndRestore(prepared, rollback);
            throw;
        }

        if (!committed.MayBeCommitted)
        {
            RestorePurchaseSnapshot(rollback);
            message = "강화 구매 결과를 확정하지 못했습니다.";
            Debug.LogError(
                "meta-upgrade-purchase-outcome-commit-failed:"
                + committed.Detail);
            return false;
        }

        PublishPostCommitUpgradeEvent(upgradeId, message);
        return true;
    }

    public int GetStartingFacilityCandidateBonus() => MetaProgressionEffects.GetIntegerBonus(OwnedState, MetaUpgradeEffectIds.StartingFacilityCandidates);
    public int GetStartingOwnerTraitCandidateBonus() => MetaProgressionEffects.GetIntegerBonus(OwnedState, MetaUpgradeEffectIds.StartingOwnerTraitCandidates);
    public float GetOwnerMaxHealthMultiplier() => MetaProgressionEffects.GetMultiplier(OwnedState, MetaUpgradeEffectIds.OwnerMaxHealth);
    public float GetInvasionWarningThresholdMultiplier() => MetaProgressionEffects.GetMultiplier(OwnedState, MetaUpgradeEffectIds.InvasionWarningThreshold);
    public float GetCommerceStockCostMultiplier(bool eligibleCategory) => eligibleCategory ? MetaProgressionEffects.GetMultiplier(OwnedState, MetaUpgradeEffectIds.CommerceStockCost) : 1f;
    public float GetFortressFacilityCostMultiplier(bool defenseFacility) => defenseFacility ? MetaProgressionEffects.GetMultiplier(OwnedState, MetaUpgradeEffectIds.FortressDefenseFacilityCost) : 1f;
    public float GetArcaneResearchWorkMultiplier() => MetaProgressionEffects.GetMultiplier(OwnedState, MetaUpgradeEffectIds.ArcaneResearchWork);
    public bool IsRecipePreserved(string id) => !string.IsNullOrWhiteSpace(id) && OwnedState.PreservedRecipeIds.Contains(id);
    public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<MetaFacilityCandidateSnapshot> candidates)
    {
        int count = MetaProgressionEffects.GetIntegerBonus(OwnedState, MetaUpgradeEffectIds.BasicPurchaseEntries);
        return count <= 0 ? Array.Empty<int>() : (candidates ?? Array.Empty<MetaFacilityCandidateSnapshot>())
            .Where(item => item.Eligible).OrderBy(item => item.DefinitionId).Take(count).Select(item => item.DefinitionId).ToArray();
    }

    public void RecordOffenseSuccess() => OwnedRunProgress.RecordOffenseSuccess();
    public void RecordOperatingDayStarted(int day) { if (day <= 1 && HasEnded) StartNewRun(); OwnedRunProgress.RecordOperatingDayStarted(day); }
    public void RecordOperatingDayReport(int day) => OwnedRunProgress.RecordOperatingDayReport(day);
    public void RecordThreat(InvasionThreatStage stage, float threat) => OwnedRunProgress.RecordThreat(stage, threat);
    public void RecordInvasionResolved(bool defended) => OwnedRunProgress.RecordInvasionResolved(defended);
    public void RecordFacilityDiscovery(int id) => OwnedRunProgress.RecordFacilityDiscovery(id);
    public void RecordResearchRecipes(IEnumerable<string> ids) => OwnedRunProgress.RecordRecipes(ids);
    public void RecordSynthesis(string recipeId, int resultBuildingId) => OwnedRunProgress.RecordSynthesis(recipeId, resultBuildingId);

    public IRunCompletionPreparedState Prepare(OwnerRunCompletionFactData fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        if (HasEnded || PendingCompletion != null)
            throw new InvalidOperationException("meta-completion-already-prepared-or-ended");
        fact.Validate();
        var result = BuildResult(fact.displayName, fact.reason, fact.outcome);
        var snapshot = new MetaRunCompletionSnapshot(fact, checked(OwnedState.CompletedRunCount + 1),
            result, OwnedRunProgress.UnlockedRecipeIds,
            MetaProgressionEffects.GetIntegerBonus(OwnedState, MetaUpgradeEffectIds.PreservedRecipeSlots));
        return new PreparedCompletion(this, snapshot);
    }

    private RunResultSnapshot BuildResult(string ownerName, string reason, DungeonRunOutcome outcome)
    {
        var result = runResultBuilder.Build(OwnedRunProgress.CreateResultContext(
            ownerName, reason, applicationPort.CaptureRunEnvironment(), outcome));
        return result.WithLegacyCurrency(MetaProgressionCalculator.CalculateLegacyCurrency(result));
    }

    private sealed class PreparedCompletion : IRunCompletionPreparedState
    {
        private readonly MetaProgressionRuntime owner;
        private readonly MetaRunCompletionSnapshot snapshot;
        private bool published, cancelled;
        internal PreparedCompletion(MetaProgressionRuntime owner, MetaRunCompletionSnapshot snapshot)
        { this.owner = owner; this.snapshot = snapshot; }
        public void Publish()
        {
            if (cancelled || published || owner.HasEnded || owner.PendingCompletion != null
                || snapshot.RunSequence != checked(owner.OwnedState.CompletedRunCount + 1))
                throw new InvalidOperationException("meta-completion-preparation-stale");
            owner.WritableLifecycle.PendingCompletion = snapshot;
            published = true;
        }
        public void Cancel()
        {
            if (cancelled) return;
            if (published)
            {
                if (owner.HasEnded || !ReferenceEquals(owner.PendingCompletion, snapshot))
                    throw new InvalidOperationException("meta-completion-cannot-cancel-committed-state");
                owner.WritableLifecycle.PendingCompletion = null;
            }
            cancelled = true;
        }
    }

    public RunResultSnapshot EndRun(string ownerName, string reason, DungeonRunOutcome outcome = DungeonRunOutcome.Defeat)
    {
        if (HasEnded && LatestResult != null) return LatestResult;
        MetaRunCompletionSnapshot pending = PendingCompletion;
        if (pending != null && (!pending.Matches(ownerName, reason, outcome)
            || pending.RunSequence != checked(OwnedState.CompletedRunCount + 1)))
            throw new InvalidOperationException("meta-completion-settlement-conflict");
        RunResultSnapshot result = pending?.Result ?? BuildResult(ownerName, reason, outcome);

        if (domainFacts == null)
            throw Missing(nameof(IDomainFactPublisher));
        int runSequence = checked(OwnedState.CompletedRunCount + 1);
        int absoluteDay = pending?.Fact.absoluteDay ?? Math.Max(1, OwnedRunProgress.CurrentDay);
        var proposedFact = new MetaRunResultDomainFact(
            runSequence,
            absoluteDay,
            result);
        if (!domainFacts.TryReserve(
                proposedFact,
                out DomainFactLease prepared,
                out string reserveFailure))
        {
            throw new InvalidOperationException(
                "Run result outcome reservation failed: " + reserveFailure);
        }

        PurchaseRollbackSnapshot stateBefore = CapturePurchaseSnapshot();
        MetaRunLifecycleAggregateState lifecycleBefore = new()
        {
            Ended = Lifecycle.Ended,
            LatestResult = Lifecycle.LatestResult,
            PendingCompletion = pending
        };
        try
        {
            WritableLifecycle.Ended = true;
            OwnedState.AddCurrency(result.legacyCurrency);
            OwnedState.RecordRunCompleted();
            int slots = pending?.PreservedRecipeSlots ?? MetaProgressionEffects.GetIntegerBonus(
                OwnedState,
                MetaUpgradeEffectIds.PreservedRecipeSlots);
            OwnedState.PreserveRecipes(
                (pending != null ? pending.RecipeCandidates : OwnedRunProgress.UnlockedRecipeIds).OrderBy(
                    id => id,
                    StringComparer.Ordinal),
                slots);
            WritableLifecycle.LatestResult = result;

            DomainFactDelivery committed = domainFacts.Publish(
                prepared,
                new MetaRunResultDomainFact(
                    runSequence,
                    absoluteDay,
                    result));
            if (!committed.MayBeCommitted)
            {
                throw new InvalidOperationException(
                    "Run result outcome commit failed: "
                    + committed.Detail);
            }
            WritableLifecycle.PendingCompletion = null;
        }
        catch
        {
            domainFacts.Cancel(prepared);
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
        DomainFactLease prepared,
        PurchaseRollbackSnapshot rollback)
    {
        try
        {
            domainFacts.Cancel(prepared);
        }
        finally
        {
            RestorePurchaseSnapshot(rollback);
        }
    }

    private PurchaseRollbackSnapshot CapturePurchaseSnapshot() => new(
        OwnedState.LifetimeEarnedCurrency,
        OwnedState.SpentCurrency,
        OwnedState.UpgradeLevels.ToArray(),
        OwnedState.PreservedRecipeIds.ToArray(),
        OwnedState.CompletedRunCount);

    private void RestorePurchaseSnapshot(PurchaseRollbackSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        OwnedState.Restore(
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
