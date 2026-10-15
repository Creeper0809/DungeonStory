using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Reserves the migrated runtime's next native revision, then publishes only a
/// resolved command whose frozen execution context matches that reservation.
/// </summary>
public sealed class OffenseBattleCommandFactObserver :
    IDurableDomainFactObserver<OffenseBattleCommandFact>,
    IDurableDomainFactRecoveryObserver<OffenseBattleCommandFact>,
    IDomainFactObserverBinding
{
    private readonly MigratedProducerOutcomeRuntime runtime;
    private readonly GameplayDomainFactCommitGroup commits;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(OffenseBattleCommandFact) });

    public OffenseBattleCommandFactObserver(
        MigratedProducerOutcomeRuntime runtime,
        GameplayDomainFactCommitGroup commits)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
        this.commits = commits
            ?? throw new ArgumentNullException(nameof(commits));
    }

    public IDisposable Connect(DomainFactDispatcher dispatcher) =>
        (dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)))
        .Subscribe("log:offense.battle-command", this);

    public bool TryReserve(
        in OffenseBattleCommandFact fact,
        out object reservation,
        out string failure)
    {
        reservation = null;
        if (Snapshot(fact).resolved)
        {
            failure = "offense-battle-command-observation-proposal-required";
            return false;
        }

        MigratedDomainFactCapture capture =
            OffenseBattleCommandFactMapping.Capture(fact);
        if (!runtime.TryReserveObserved(
                capture,
                out PreparedMigratedProducerOutcome prepared,
                out failure))
        {
            return false;
        }

        reservation = new Reservation(
            OffenseBattleCommandFactMapping.ReservationDigest(fact),
            prepared);
        return true;
    }

    public bool TryPrepare(
        in OffenseBattleCommandFact fact,
        object reservation,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (!Snapshot(fact).resolved)
        {
            failure = "offense-battle-command-unresolved-publication";
            return false;
        }
        if (reservation is not Reservation state
            || state.Publication != null
            || !string.Equals(
                state.ContextDigest,
                OffenseBattleCommandFactMapping.ReservationDigest(fact),
                StringComparison.Ordinal))
        {
            failure =
                "offense-battle-command-observation-reservation-conflict";
            return false;
        }

        MigratedDomainFactCapture capture =
            OffenseBattleCommandFactMapping.Capture(fact);
        if (!runtime.TryPrepareObserved(
                state.Prepared,
                capture.ResolveDetails(state.Prepared.ResultKey),
                commits,
                out GameplayDomainFactPublication publication,
                out failure))
        {
            return false;
        }

        state.Publication = publication;
        prepared = publication;
        return true;
    }

    public DomainFactDelivery Replay(in OffenseBattleCommandFact fact) =>
        !Snapshot(fact).resolved
            ? DomainFactDelivery.Reject(
                "offense-battle-command-unresolved-replay")
            : DomainFactDelivery.Reject(
                "offense-battle-command-saved-publication-required");

    public bool TryPrepareReplay(
        string publicationKey,
        in OffenseBattleCommandFact fact,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (!Snapshot(fact).resolved)
        {
            failure = "offense-battle-command-unresolved-replay";
            return false;
        }

        bool accepted = runtime.TryPrepareObservedRecovery(
            OffenseBattleCommandFactMapping.Capture(fact),
            publicationKey,
            commits,
            out GameplayDomainFactPublication publication,
            out failure);
        prepared = publication;
        return accepted;
    }

    public void Cancel(object reservation)
    {
        if (reservation is not Reservation state)
            return;
        state.Publication?.Cancel();
        runtime.Cancel(state.Prepared);
    }

    private static OffenseBattleCommandFactData Snapshot(
        OffenseBattleCommandFact fact) =>
        (fact ?? throw new ArgumentNullException(nameof(fact))).Snapshot();

    private sealed class Reservation : IDomainFactReservedIdentity
    {
        internal Reservation(
            string contextDigest,
            PreparedMigratedProducerOutcome prepared)
        {
            ContextDigest = contextDigest;
            Prepared = prepared;
        }

        internal string ContextDigest { get; }
        internal PreparedMigratedProducerOutcome Prepared { get; }
        internal GameplayDomainFactPublication Publication { get; set; }
        public string PublicationKey =>
            Publication?.OperationKey ?? Prepared.ResultKey.ToString();
    }
}
