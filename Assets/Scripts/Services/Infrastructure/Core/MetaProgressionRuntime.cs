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

    public MetaProgressionState State => state ?? throw Missing(nameof(IMetaUpgradeDefinitionCatalog));
    public RunResultSnapshot LatestResult => Lifecycle.LatestResult;
    public MetaRunProgressTracker RunProgress => runProgress ?? throw Missing(nameof(MetaRunProgressTracker));
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

        if (domainFacts == null)
        {
            message = "강화 구매 결과 기록이 준비되지 않았습니다.";
            Debug.LogError("meta-upgrade-purchase-domain-fact-publisher-unavailable");
            return false;
        }

        string upgradeId = definition.id;
        int previousLevel = State.GetUpgradeLevel(upgradeId);
        int absoluteDay = Math.Max(1, RunProgress.CurrentDay);
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

        DomainFactDelivery committed;
        try
        {
            committed = domainFacts.Publish(
                prepared,
                new MetaUpgradePurchaseDomainFact(
                    upgradeId,
                    definition.title,
                    previousLevel,
                    State.GetUpgradeLevel(upgradeId),
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

    public IRunCompletionPreparedState Prepare(OwnerRunCompletionFactData fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        if (HasEnded || PendingCompletion != null)
            throw new InvalidOperationException("meta-completion-already-prepared-or-ended");
        fact.Validate();
        var result = BuildResult(fact.displayName, fact.reason, fact.outcome);
        var snapshot = new MetaRunCompletionSnapshot(fact, checked(State.CompletedRunCount + 1),
            result, RunProgress.UnlockedRecipeIds,
            MetaProgressionEffects.GetIntegerBonus(State, MetaUpgradeEffectIds.PreservedRecipeSlots));
        return new PreparedCompletion(this, snapshot);
    }

    private RunResultSnapshot BuildResult(string ownerName, string reason, DungeonRunOutcome outcome)
    {
        var result = runResultBuilder.Build(RunProgress.CreateResultContext(
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
                || snapshot.RunSequence != checked(owner.State.CompletedRunCount + 1))
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
            || pending.RunSequence != checked(State.CompletedRunCount + 1)))
            throw new InvalidOperationException("meta-completion-settlement-conflict");
        RunResultSnapshot result = pending?.Result ?? BuildResult(ownerName, reason, outcome);

        if (domainFacts == null)
            throw Missing(nameof(IDomainFactPublisher));
        int runSequence = checked(State.CompletedRunCount + 1);
        int absoluteDay = pending?.Fact.absoluteDay ?? Math.Max(1, RunProgress.CurrentDay);
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
            State.AddCurrency(result.legacyCurrency);
            State.RecordRunCompleted();
            int slots = pending?.PreservedRecipeSlots ?? MetaProgressionEffects.GetIntegerBonus(
                State,
                MetaUpgradeEffectIds.PreservedRecipeSlots);
            State.PreserveRecipes(
                (pending != null ? pending.RecipeCandidates : RunProgress.UnlockedRecipeIds).OrderBy(
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
