using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Enforces the single historical result identity for an expedition before the
/// migrated outcome observer crosses its durable boundary.
/// </summary>
public sealed class OffenseExpeditionResultFactObserver :
    IDurableDomainFactObserver<OffenseExpeditionResolvedFact>,
    IDurableDomainFactRecoveryObserver<OffenseExpeditionResolvedFact>,
    IDomainFactObserverBinding
{
    private readonly MigratedProducerOutcomeRuntime runtime;
    private readonly GameplayDomainFactCommitGroup commits;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(OffenseExpeditionResolvedFact) });

    public OffenseExpeditionResultFactObserver(
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
        .Subscribe("log:offense.expedition-result", this);

    public static GameplayResultKey Key(OffenseExpeditionResolvedFact fact)
    {
        OffenseExpeditionResolvedFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        return MigratedProducerOutcomeCatalog.ResultKey(
            MigratedProducerOutcomeKind.OffenseExpeditionResult,
            "offense-expedition-result:" + value.Result.expeditionId,
            1L);
    }

    public bool TryReserve(
        in OffenseExpeditionResolvedFact fact,
        out object reservation,
        out string failure)
    {
        reservation = null;
        MigratedDomainFactCapture capture =
            OffenseExpeditionResultFactMapping.Capture(fact);
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
            failure = "offense-expedition-result-observation-already-applied";
            return false;
        }

        reservation = new Reservation(
            OffenseExpeditionResultFactMapping.Digest(fact),
            prepared);
        return true;
    }

    public bool TryPrepare(
        in OffenseExpeditionResolvedFact fact,
        object reservation,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (reservation is not Reservation state
            || state.Publication != null
            || !string.Equals(
                state.Digest,
                OffenseExpeditionResultFactMapping.Digest(fact),
                StringComparison.Ordinal))
        {
            failure = "offense-expedition-result-observation-reservation-conflict";
            return false;
        }

        MigratedDomainFactCapture capture =
            OffenseExpeditionResultFactMapping.Capture(fact);
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

    public DomainFactDelivery Replay(in OffenseExpeditionResolvedFact fact) =>
        DomainFactDelivery.Reject(
            "offense-expedition-result-saved-publication-required");

    public bool TryPrepareReplay(
        string publicationKey,
        in OffenseExpeditionResolvedFact fact,
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
            failure = "offense-expedition-result-observation-identity-conflict";
            return false;
        }

        bool accepted = runtime.TryPrepareObservedRecovery(
            OffenseExpeditionResultFactMapping.Capture(fact),
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
