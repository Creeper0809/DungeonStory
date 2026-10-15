using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Reserves the sole revision-one narrative identity for each terminal arrival
/// settlement and requires saved identity for all recovery.
/// </summary>
public sealed class OffenseArrivalSettlementFactObserver :
    IDurableDomainFactObserver<OffenseArrivalSettledFact>,
    IDurableDomainFactRecoveryObserver<OffenseArrivalSettledFact>,
    IDomainFactObserverBinding
{
    private readonly MigratedProducerOutcomeRuntime runtime;
    private readonly GameplayDomainFactCommitGroup commits;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(OffenseArrivalSettledFact) });

    public OffenseArrivalSettlementFactObserver(
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
        .Subscribe("log:offense.arrival-settled", this);

    public static GameplayResultKey Key(OffenseArrivalSettledFact fact)
    {
        OffenseArrivalSettledFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        return MigratedProducerOutcomeCatalog.ResultKey(
            MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt,
            value.Snapshot().arrivalId,
            1L);
    }

    public bool TryReserve(
        in OffenseArrivalSettledFact fact,
        out object reservation,
        out string failure)
    {
        reservation = null;
        MigratedDomainFactCapture capture =
            OffenseArrivalSettlementFactMapping.Capture(fact);
        if (!runtime.TryReserveObserved(
                capture,
                out PreparedMigratedProducerOutcome prepared,
                out failure))
        {
            return false;
        }

        if (prepared.ResultKey != Key(fact))
        {
            runtime.Cancel(prepared);
            failure = "offense-arrival-settlement-observation-already-applied";
            return false;
        }

        reservation = new Reservation(
            OffenseArrivalSettlementFactMapping.Digest(fact),
            prepared);
        return true;
    }

    public bool TryPrepare(
        in OffenseArrivalSettledFact fact,
        object reservation,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (reservation is not Reservation state
            || state.Publication != null
            || !string.Equals(
                state.Digest,
                OffenseArrivalSettlementFactMapping.Digest(fact),
                StringComparison.Ordinal))
        {
            failure = "offense-arrival-settlement-observation-reservation-conflict";
            return false;
        }

        MigratedDomainFactCapture capture =
            OffenseArrivalSettlementFactMapping.Capture(fact);
        if (!runtime.TryPrepareObserved(
                state.Prepared,
                capture.ResolveDetails(Key(fact)),
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

    public DomainFactDelivery Replay(in OffenseArrivalSettledFact fact) =>
        DomainFactDelivery.Reject(
            "offense-arrival-settlement-saved-publication-required");

    public bool TryPrepareReplay(
        string publicationKey,
        in OffenseArrivalSettledFact fact,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        string expectedKey = Key(fact).ToString();
        if (!string.Equals(
                publicationKey,
                expectedKey,
                StringComparison.Ordinal))
        {
            failure = "offense-arrival-settlement-observation-identity-conflict";
            return false;
        }

        bool accepted = runtime.TryPrepareObservedRecovery(
            OffenseArrivalSettlementFactMapping.Capture(fact),
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

    private sealed class Reservation : IDomainFactReservedIdentity
    {
        internal Reservation(
            string digest,
            PreparedMigratedProducerOutcome prepared)
        {
            Digest = digest;
            Prepared = prepared;
        }

        internal string Digest { get; }
        internal PreparedMigratedProducerOutcome Prepared { get; }
        internal GameplayDomainFactPublication Publication { get; set; }
        public string PublicationKey =>
            Publication?.OperationKey ?? Prepared.ResultKey.ToString();
    }
}
