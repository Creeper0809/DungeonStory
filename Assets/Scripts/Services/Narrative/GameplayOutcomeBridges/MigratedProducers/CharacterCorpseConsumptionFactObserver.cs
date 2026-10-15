using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

/// <summary>Observes one actual corpse meal as an atomic physical result and personal experience.</summary>
public sealed class CharacterCorpseConsumptionFactObserver :
    IDurableDomainFactObserver<CharacterCorpseConsumedFact>,
    IDurableDomainFactStagingObserver<CharacterCorpseConsumedFact>,
    IDurableDomainFactRecoveryObserver<CharacterCorpseConsumedFact>, IDomainFactCommitGroup
{
#if UNITY_EDITOR
    internal static Func<GameplayResultKey, bool> DebugRejectCommit { get; set; }
#endif
    private readonly GameplayDomainFactObserver<PhysicalItemDisposedFact, TradeInventoryOutcomeReceipt> physical;
    private readonly MigratedProducerOutcomeRuntime migrated;
    private readonly GameplayDomainFactCommitGroup commits;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    public CharacterCorpseConsumptionFactObserver(IGameplayOutcomeRegistry registry, GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder, MigratedProducerOutcomeRuntime migrated, GameplayDomainFactCommitGroup commits)
    {
        physical = new(PhysicalItemDispositionFactObservation.Capture, registry, ledger, recorder, commits);
        this.migrated = migrated ?? throw new ArgumentNullException(nameof(migrated));
        this.commits = commits ?? throw new ArgumentNullException(nameof(commits));
        diagnostics = ledger ?? throw new ArgumentNullException(nameof(ledger));
    }

    public bool TryStage(in CharacterCorpseConsumedFact fact, out object reservation,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        reservation = null; prepared = null;
        if (!physical.TryStage(fact.Physical, out _, out var first, out failure)) return false;
        var primary = (GameplayDomainFactPublication)first;
        if (primary.IsReplay)
        { primary.Cancel(); failure = "corpse-existing-publication-requires-owner-recovery"; return false; }
        PreparedMigratedProducerOutcome second = default;
        try
        {
            var captured = Capture(fact, primary);
            if (!migrated.TryReserveObserved(captured, out second, out failure)) { primary.Cancel(); return false; }
            if (!migrated.TryPrepareObserved(second, captured.ResolveDetails(second.ResultKey), commits,
                    out var secondary, out failure))
            { migrated.Cancel(second); primary.Cancel(); return false; }
            reservation = prepared = new Pair(this, primary, secondary);
            return true;
        }
        catch
        {
            if (second.IsValid) migrated.Cancel(second);
            primary.Cancel(); throw;
        }
    }

    public bool TryPrepareReplay(string publicationKey, in CharacterCorpseConsumedFact fact,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        if (!GameplayDomainFactPairProof.TryReadKeys(publicationKey, out string firstKey, out string secondKey))
        { failure = "corpse-publication-reference-invalid"; return false; }
        if (!physical.TryPrepareReplay(firstKey, fact.Physical, out var first, out failure)) return false;
        var primary = (GameplayDomainFactPublication)first;
        try
        {
            if (!migrated.TryPrepareObservedRecovery(Capture(fact, primary), secondKey, commits,
                    out var secondary, out failure)) { primary.Cancel(); return false; }
            var pair = new Pair(this, primary, secondary);
            if (pair.OperationKey != publicationKey)
            { pair.Cancel(); failure = "corpse-publication-reference-conflict"; return false; }
            prepared = pair; return true;
        }
        catch { primary.Cancel(); throw; }
    }

    private MigratedDomainFactCapture Capture(CharacterCorpseConsumedFact fact, GameplayDomainFactPublication primary)
    {
        GameplayOutcomeId parent;
        GameplayResultKey key = primary.IsReplay ? primary.ReplayKey : primary.Prepared.ResultKey;
        if (!primary.IsReplay) parent = primary.Prepared.Token.OutcomeId;
        else
        {
            if (!diagnostics.TryGetResultIdentity(key, out var identity)
                || identity.ResultKey != key || identity.State < GameplayOutcomeReplayState.Committed
                || !identity.OutcomeId.IsValid)
                throw new InvalidOperationException("corpse-physical-parent-identity-unavailable");
            parent = identity.OutcomeId;
        }
        return CharacterCorpseConsumptionFactObservation.Capture(fact, key, parent);
    }

    public bool Commit(IReadOnlyList<IDomainFactPreparedPublication> publications,
        DomainFactDelivery[] results, out string failure)
    {
        if (publications?.Count != 1 || results?.Length != 1 || publications[0] is not Pair pair
            || pair.CommitGroup != this || pair.Terminal)
            throw new ArgumentException("corpse-publication-cohort-invalid");
#if UNITY_EDITOR
        var physicalKey = pair.First.IsReplay ? pair.First.ReplayKey : pair.First.Prepared.ResultKey;
        if (DebugRejectCommit?.Invoke(physicalKey) == true)
        {
            pair.Cancel(); failure = "qa-corpse-consumption-atomic-commit-rejected";
            results[0] = DomainFactDelivery.Reject(failure); return false;
        }
#endif
        pair.Terminal = true;
        var deliveries = new DomainFactDelivery[2];
        try
        {
            bool accepted = commits.Commit(new IDomainFactPreparedPublication[] { pair.First, pair.Second }, deliveries, out failure);
            results[0] = GameplayDomainFactPairProof.Combine(pair.OperationKey, deliveries[0], deliveries[1]);
            return accepted;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        {
            // Publication may already be durable. The frozen domain owner drives recovery.
            pair.First.Cancel(); pair.Second.Cancel();
            failure = "corpse-publication-commit-uncertain:" + exception.GetType().Name;
            results[0] = new DomainFactDelivery(DomainFactDeliveryPhase.InDoubt, pair.OperationKey, null, null, failure);
            return true;
        }
    }

    public bool TryReserve(in CharacterCorpseConsumedFact proposed, out object reservation, out string failure)
    {
        reservation = null; failure = "corpse-publication-requires-exact-staging"; return false;
    }
    public bool TryPrepare(in CharacterCorpseConsumedFact actual, object reservation,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        // Corpse consumption uses exact staging, never a mutable proposal/actual pair.
        prepared = null; failure = "corpse-publication-requires-exact-staging"; return false;
    }
    public DomainFactDelivery Replay(in CharacterCorpseConsumedFact fact)
        => DomainFactDelivery.Reject("corpse-replay-requires-saved-publication-reference");
    public void Cancel(object reservation) { if (reservation is Pair pair) pair.Cancel(); }

    private sealed class Pair : IDomainFactPreparedPublication, IDomainFactPreparedIdentity
    {
        public Pair(CharacterCorpseConsumptionFactObserver owner, GameplayDomainFactPublication first,
            GameplayDomainFactPublication second)
        {
            CommitGroup = owner; First = first; Second = second;
            OperationKey = GameplayDomainFactPairProof.Key(first.OperationKey, second.OperationKey);
        }
        public GameplayDomainFactPublication First { get; }
        public GameplayDomainFactPublication Second { get; }
        public IDomainFactCommitGroup CommitGroup { get; }
        public string OperationKey { get; }
        public long OwnerRevision => First.OwnerRevision;
        public bool IsReplay => First.IsReplay && Second.IsReplay;
        public bool Terminal { get; set; }
        public void Cancel()
        {
            if (Terminal) return;
            Terminal = true; First.Cancel(); Second.Cancel();
        }
    }
}

public sealed class CharacterCorpseConsumptionFactBinding : IDomainFactObserverBinding
{
    private readonly CharacterCorpseConsumptionFactObserver observer;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(CharacterCorpseConsumedFact) });
    public CharacterCorpseConsumptionFactBinding(CharacterCorpseConsumptionFactObserver observer) => this.observer = observer;
    public IDisposable Connect(DomainFactDispatcher dispatcher) => dispatcher.Subscribe("survival.corpse-consumption-observer", observer);
}
