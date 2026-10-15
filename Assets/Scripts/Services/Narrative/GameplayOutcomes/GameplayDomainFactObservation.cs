using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>The durable subscriber owns receipt creation and all ledger details.</summary>
public sealed class GameplayDomainFactObserver<TFact, TReceipt> : IDurableDomainFactObserver<TFact>,
    IDurableDomainFactStagingObserver<TFact>, IDurableDomainFactRecoveryObserver<TFact>
{
    private readonly Func<TFact, TReceipt> capture;
    private readonly IGameplayOutcomeAdapter<TReceipt> adapter;
    private readonly GameplayOutcomeLedger ledger;
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly GameplayDomainFactCommitGroup commits;
    private readonly Func<TFact, long, OutcomeWriteRequirements> reserveShape;
    private readonly Func<TFact, TFact, bool> matchesProposal;

    public GameplayDomainFactObserver(Func<TFact, TReceipt> capture,
        IGameplayOutcomeRegistry registry, GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder, GameplayDomainFactCommitGroup commits,
        Func<TFact, long, OutcomeWriteRequirements> reserveShape = null,
        Func<TFact, TFact, bool> matchesProposal = null)
    {
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        if (!registry.TryGetAdapter<TReceipt>(out adapter))
            throw new InvalidOperationException("A domain observer has no registered receipt adapter.");
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.commits = commits ?? throw new ArgumentNullException(nameof(commits));
        this.reserveShape = reserveShape;
        this.matchesProposal = matchesProposal;
    }

    public bool TryReserve(in TFact proposed, out object reservation, out string failure)
    {
        reservation = null;
        OutcomeWriteRequirements requirements;
        if (reserveShape != null)
        {
            // Some domain proposals precede irreversible item consumption.
            // Reserve their bounded shape without fabricating a completed
            // receipt; capture/adapter validation still runs on actual facts.
            requirements = reserveShape(proposed, ledger.CurrentWorldEpoch);
        }
        else
        {
            TReceipt receipt = capture(proposed);
            OutcomePrepareResult measured = adapter.TryGetRequirements(receipt, ledger.CurrentWorldEpoch,
                out requirements);
            if (!measured.Success)
            { failure = "fact-observer-shape:" + measured.Code + ":" + measured.DetailCode; return false; }
        }
        OutcomePrepareResult result = recorder.TryReserve(requirements, out var reserved);
        if (!result.Success)
        { failure = "fact-observer-reserve:" + result.Code + ":" + result.DetailCode; return false; }
        reservation = new Reservation(reserved) { PublicationKey = requirements.ResultKey.ToString(), Proposed = proposed };
        failure = string.Empty;
        return true;
    }

    public bool TryPrepare(in TFact actual, object reservation,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        if (reservation is not Reservation state || state.Prepared.IsValid)
        { failure = "fact-observer-reservation-invalid"; return false; }
        if (matchesProposal != null && !matchesProposal(actual, state.Proposed))
        { failure = "fact-observer-reserved-fact-drift"; return false; }
        TReceipt receipt = capture(actual);
        OutcomePrepareResult measured = adapter.TryGetRequirements(receipt, ledger.CurrentWorldEpoch,
            out var requirements);
        if (!measured.Success)
        { failure = "fact-observer-shape:" + measured.Code + ":" + measured.DetailCode; return false; }
        if (requirements.ResultKey.ToString() != state.PublicationKey)
        { failure = "fact-observer-reserved-identity-drift"; return false; }
        OutcomePrepareResult result = recorder.TryWriteReserved(receipt, state.Reserved, out var token);
        if (!result.Success)
        { failure = "fact-observer-write:" + result.Code + ":" + result.DetailCode; return false; }
        state.Prepared = token;
        prepared = new GameplayDomainFactPublication(commits, recorder,
            new PreparedOwnerOutcome(token), requirements.OwnerRevision);
        failure = string.Empty;
        return true;
    }

    public bool TryStage(in TFact fact, out object reservation,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        reservation = null;
        prepared = null;
        TReceipt receipt = capture(fact);
        var measured = adapter.TryGetRequirements(receipt, ledger.CurrentWorldEpoch, out var requirements);
        if (!measured.Success)
        { failure = "fact-observer-exact-shape:" + measured.Code + ":" + measured.DetailCode; return false; }
        var result = recorder.TryPrepare(receipt, out var token);
        if (result.Success)
        {
            reservation = new Reservation(default) { Prepared = token };
            prepared = new GameplayDomainFactPublication(commits, recorder,
                new PreparedOwnerOutcome(token), requirements.OwnerRevision);
        }
        else if (result.Code is OutcomePrepareCode.AlreadyCommitted or OutcomePrepareCode.AlreadyPublished
            or OutcomePrepareCode.AlreadyTerminal)
        {
            // TryPrepare has compared the complete canonical payload; no
            // domain effect is repeated just because the key already exists.
            reservation = new Reservation(default);
            prepared = new GameplayDomainFactPublication(commits, recorder,
                requirements.ResultKey, requirements.OwnerRevision);
        }
        else
        { failure = "fact-observer-exact-prepare:" + result.Code + ":" + result.DetailCode; return false; }
        failure = string.Empty;
        return true;
    }

    public DomainFactDelivery Replay(in TFact fact)
    {
        TReceipt receipt = capture(fact);
        if (!adapter.TryGetRequirements(receipt, ledger.CurrentWorldEpoch, out var requirements).Success)
            return DomainFactDelivery.Reject("fact-observer-replay-shape-invalid");
        // TryPrepare validates the canonical payload even for an existing key.
        OutcomePrepareResult result = recorder.TryPrepare(receipt, out var token);
        if (result.Success)
        {
            var publications = new IDomainFactPreparedPublication[]
            {
                new GameplayDomainFactPublication(commits, recorder,
                    new PreparedOwnerOutcome(token), requirements.OwnerRevision)
            };
            var deliveries = new DomainFactDelivery[1];
            commits.Commit(publications, deliveries, out _);
            return deliveries[0];
        }
        if (result.Code is OutcomePrepareCode.AlreadyCommitted or OutcomePrepareCode.AlreadyPublished
            or OutcomePrepareCode.AlreadyTerminal)
            return commits.Replay(requirements.ResultKey);
        return DomainFactDelivery.Reject("fact-observer-replay:" + result.Code + ":" + result.DetailCode);
    }

    public bool TryPrepareReplay(string publicationKey, in TFact fact,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        TReceipt receipt = capture(fact);
        var measured = adapter.TryGetRequirements(receipt, ledger.CurrentWorldEpoch, out var requirements);
        if (!measured.Success || requirements.ResultKey.ToString() != publicationKey)
        { failure = "fact-observer-recovery-identity-mismatch"; return false; }
        return TryStage(fact, out _, out prepared, out failure);
    }

    public void Cancel(object reservation)
    {
        if (reservation is not Reservation state) return;
        if (state.Prepared.IsValid) recorder.CancelPrepared(state.Prepared);
        recorder.CancelReservation(state.Reserved);
    }

    private sealed class Reservation : IDomainFactReservedIdentity
    {
        public Reservation(PreparedOutcomeReservation reserved) => Reserved = reserved;
        public PreparedOutcomeReservation Reserved { get; }
        public PreparedOutcomeToken Prepared;
        public string PublicationKey { get; set; }
        public TFact Proposed;
    }
}

internal interface IGameplayDomainFactRevision
{
    string ConflictKey { get; }
    bool Validate();
    void Apply();
    void Rollback();
}

public sealed class GameplayDomainFactPublication : IDomainFactPreparedPublication, IDomainFactPreparedIdentity
{
    private readonly IGameplayOutcomeRecorder recorder;
    public GameplayDomainFactPublication(GameplayDomainFactCommitGroup group,
        IGameplayOutcomeRecorder recorder, PreparedOwnerOutcome prepared, long ownerRevision)
    { CommitGroup = group; this.recorder = recorder; Prepared = prepared; OwnerRevision = ownerRevision; }
    internal GameplayDomainFactPublication(GameplayDomainFactCommitGroup group,
        IGameplayOutcomeRecorder recorder, GameplayResultKey replayKey, long ownerRevision)
    { CommitGroup = group; this.recorder = recorder; ReplayKey = replayKey; OwnerRevision = ownerRevision; IsReplay = true; }
    public IDomainFactCommitGroup CommitGroup { get; }
    internal PreparedOwnerOutcome Prepared { get; }
    internal GameplayResultKey ReplayKey { get; }
    public long OwnerRevision { get; }
    public bool IsReplay { get; }
    public string OperationKey => (IsReplay ? ReplayKey : Prepared.ResultKey).ToString();
    internal IGameplayDomainFactRevision Revision { get; set; }
    public void Cancel() { if (!IsReplay) recorder.CancelPrepared(Prepared.Token); }
}

public sealed class GameplayDomainFactCommitGroup : IDomainFactCommitGroup
{
    private readonly PreparedOutcomeOwnerTransaction transactions;
    public GameplayDomainFactCommitGroup(IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics) =>
        transactions = new PreparedOutcomeOwnerTransaction(recorder, diagnostics);

    public bool Commit(IReadOnlyList<IDomainFactPreparedPublication> publications,
        DomainFactDelivery[] results, out string failure)
    {
        if (publications == null || results == null || publications.Count == 0
            || results.Length != publications.Count
            || publications.Any(value => value is not GameplayDomainFactPublication
                || !ReferenceEquals(value.CommitGroup, this)))
            throw new ArgumentException("Mismatched gameplay observer commit group.");
        var values = publications.Cast<GameplayDomainFactPublication>().ToArray();
        if (values.Any(value => value.IsReplay))
        {
            if (values.Any(value => !value.IsReplay))
            {
                failure = "fact-observer-cohort-partial-replay";
                for (int i = 0; i < values.Length; i++)
                { values[i].Cancel(); results[i] = DomainFactDelivery.Reject(failure); }
                return false;
            }
            failure = string.Empty;
            for (int i = 0; i < values.Length; i++) results[i] = Replay(values[i].ReplayKey);
            // All entries were validated canonical before staging. A missing
            // identity now is uncertainty, not permission to undo domain state.
            for (int i = 0; i < values.Length; i++)
                if (!results[i].MayBeCommitted)
                    results[i] = DomainFactDelivery.Uncertain("fact-observer-canonical-replay-unresolved");
            return true;
        }
        var revisionKeys = new HashSet<string>(StringComparer.Ordinal);
        if (values.Length > GameplayOutcomeTransactionLimits.MaximumAtomicCommitBatchCount
            || values.Any(value => value.Revision != null
                && (!revisionKeys.Add(value.Revision.ConflictKey) || !value.Revision.Validate())))
        {
            failure = "fact-observer-cohort-owner-revision-invalid";
            for (int i = 0; i < values.Length; i++)
            { values[i].Cancel(); results[i] = DomainFactDelivery.Reject(failure); }
            return false;
        }
        var applied = new List<IGameplayDomainFactRevision>();
        try
        {
            foreach (var value in values)
            {
                if (value.Revision == null) continue;
                // Include the current participant before Apply so a throwing
                // copy-on-write operation is restored before any ledger commit.
                applied.Add(value.Revision);
                value.Revision.Apply();
            }
        }
        catch (Exception exception)
        {
            for (int i = applied.Count - 1; i >= 0; i--) applied[i].Rollback();
            foreach (var value in values) value.Cancel();
            failure = "fact-observer-owner-revision-apply:" + exception.GetType().Name;
            for (int i = 0; i < values.Length; i++) results[i] = DomainFactDelivery.Reject(failure);
            return false;
        }
        var committed = new OwnerOutcomeCommitResult[values.Length];
        bool success;
        if (values.Length == 1)
        {
            committed[0] = transactions.Commit(values[0].Prepared, values[0].OwnerRevision);
            success = committed[0].DurablyCommitted;
            failure = success ? string.Empty : committed[0].DetailCode;
        }
        else success = transactions.CommitBatch(values.Select(value => value.Prepared).ToArray(),
            values.Select(value => value.OwnerRevision).ToArray(), committed, out failure);
        // Never roll revisions back after an ambiguous exception at Commit.
        // Only an explicit atomic rejection establishes that nothing persisted.
        if (!success)
            for (int i = applied.Count - 1; i >= 0; i--) applied[i].Rollback();
        for (int i = 0; i < results.Length; i++)
        {
            results[i] = Convert(committed[i]);
            // The owner retained its frozen fact before Commit. Release only an
            // uncommitted preparation after an unknown response so same-process
            // replay can reserve it again. CancelPrepared cannot undo a committed
            // page, and this never authorizes rolling gameplay/revisions back.
            if (results[i].Phase == DomainFactDeliveryPhase.InDoubt) values[i].Cancel();
        }
        return success;
    }

    public DomainFactDelivery Replay(GameplayResultKey key) => Convert(transactions.Reconcile(key));

    private static DomainFactDelivery Convert(in OwnerOutcomeCommitResult result)
    {
        bool complete = result.Acknowledged && result.ResultKey.IsValid && result.OutcomeId.IsValid
            && result.CanonicalPayloadHash?.Length == 64;
        return new DomainFactDelivery(!result.DurablyCommitted ? DomainFactDeliveryPhase.Rejected
            : !result.OutcomeId.IsValid ? DomainFactDeliveryPhase.InDoubt
            : complete ? DomainFactDeliveryPhase.Delivered : DomainFactDeliveryPhase.AcceptedPendingDelivery,
            result.ResultKey.ToString(), result.OutcomeId.ToString(), result.CanonicalPayloadHash, result.DetailCode);
    }
}

public sealed class GameplayDomainFactObserverBinding<TFact, TReceipt> : IDomainFactObserverBinding
{
    private readonly string observerId;
    private readonly GameplayDomainFactObserver<TFact, TReceipt> observer;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(TFact) });
    public GameplayDomainFactObserverBinding(string observerId, Func<TFact, TReceipt> capture,
        IGameplayOutcomeRegistry registry, GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder, GameplayDomainFactCommitGroup commits,
        Func<TFact, long, OutcomeWriteRequirements> reserveShape = null,
        Func<TFact, TFact, bool> matchesProposal = null)
    {
        this.observerId = observerId;
        observer = new GameplayDomainFactObserver<TFact, TReceipt>(capture, registry, ledger, recorder, commits, reserveShape, matchesProposal);
    }
    public IDisposable Connect(DomainFactDispatcher dispatcher) => dispatcher.Subscribe(observerId, observer);
}
