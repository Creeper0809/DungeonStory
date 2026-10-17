using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Preserves the expedition node sequence as the narrative result revision and
/// only publishes a resolved fact that matches its frozen proposal context.
/// </summary>
public sealed class OffenseNodeFactObserver :
    IDurableDomainFactObserver<OffenseNodeResolutionFact>,
    IDurableDomainFactRecoveryObserver<OffenseNodeResolutionFact>,
    IDomainFactObserverBinding
{
    private readonly MigratedProducerOutcomeRuntime runtime;
    private readonly GameplayDomainFactCommitGroup commits;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(OffenseNodeResolutionFact) });

    public OffenseNodeFactObserver(
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
        .Subscribe("log:offense.node-resolved", this);

    public static GameplayResultKey Key(OffenseNodeResolutionFact fact)
    {
        OffenseNodeResolutionFactData data = Snapshot(fact);
        return MigratedProducerOutcomeCatalog.ResultKey(
            MigratedProducerOutcomeKind.OffenseExpeditionNodeResult,
            OffenseNodeFactMapping.OwnerIdentity(data),
            data.sequence);
    }

    public bool TryReserve(
        in OffenseNodeResolutionFact fact,
        out object reservation,
        out string failure)
    {
        reservation = null;
        if (Snapshot(fact).resolved)
        {
            failure = "offense-node-observation-proposal-required";
            return false;
        }

        MigratedDomainFactCapture capture = OffenseNodeFactMapping.Capture(fact);
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
            failure = "offense-node-observation-counter-mismatch";
            return false;
        }

        reservation = new Reservation(
            OffenseNodeFactMapping.ReservationDigest(fact),
            prepared);
        return true;
    }

    public bool TryPrepare(
        in OffenseNodeResolutionFact fact,
        object reservation,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (!Snapshot(fact).resolved)
        {
            failure = "offense-node-unresolved-publication";
            return false;
        }
        if (reservation is not Reservation state
            || state.Publication != null
            || !string.Equals(
                state.ContextDigest,
                OffenseNodeFactMapping.ReservationDigest(fact),
                StringComparison.Ordinal))
        {
            failure = "offense-node-observation-reservation-conflict";
            return false;
        }

        MigratedDomainFactCapture capture = OffenseNodeFactMapping.Capture(fact);
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

    public DomainFactDelivery Replay(in OffenseNodeResolutionFact fact) =>
        !Snapshot(fact).resolved
            ? DomainFactDelivery.Reject("offense-node-unresolved-replay")
            : DomainFactDelivery.Reject(
                "offense-node-saved-publication-required");

    public bool TryPrepareReplay(
        string publicationKey,
        in OffenseNodeResolutionFact fact,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (!Snapshot(fact).resolved)
        {
            failure = "offense-node-unresolved-replay";
            return false;
        }

        string expectedKey = Key(fact).ToString();
        if (!string.Equals(publicationKey, expectedKey, StringComparison.Ordinal))
        {
            failure = "offense-node-observation-identity-conflict";
            return false;
        }

        bool accepted = runtime.TryPrepareObservedRecovery(
            OffenseNodeFactMapping.Capture(fact),
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

    private static OffenseNodeResolutionFactData Snapshot(
        OffenseNodeResolutionFact fact) =>
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
