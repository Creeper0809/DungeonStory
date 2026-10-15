#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Editor-only fault probe around the real combat-action cohort observer.
/// Production reservation, preparation and commit remain owned by the wrapped observer.
/// </summary>
internal sealed class CombatActionFactProbeObserver :
    IDurableDomainFactObserver<CombatActionFact>,
    IDurableDomainFactRecoveryObserver<CombatActionFact>
{
    private static readonly MigratedProducerOutcomeKind[] CandidateKinds =
    {
        MigratedProducerOutcomeKind.CombatAttackResult,
        MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
        MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
        MigratedProducerOutcomeKind.CharacterDeathEvent,
        MigratedProducerOutcomeKind.CharacterKilledEvent
    };

    private readonly CombatActionFactObserver inner;
    private readonly MigratedProducerOutcomeProbeTransaction probe;
    private readonly ProbeCommitGroup commits;

    internal CombatActionFactProbeObserver(
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        MigratedProducerOutcomeRuntime migrated,
        GameplayDomainFactCommitGroup nativeCommits,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        inner = new CombatActionFactObserver(
            ledger,
            recorder,
            migrated,
            nativeCommits);
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        commits = new ProbeCommitGroup(
            ledger ?? throw new ArgumentNullException(nameof(ledger)),
            this.probe);
    }

    public bool TryReserve(
        in CombatActionFact proposed,
        out object reservation,
        out string failure)
    {
        reservation = null;
        if (proposed == null)
        {
            failure = "combat-action-probe-proposal-missing";
            return false;
        }

        CombatActionFactData data = proposed.Snapshot();
        int candidateCount = data.target?.kind == CombatFactParticipantKind.Character
            ? CandidateKinds.Length
            : 1;
        for (int index = 0; index < candidateCount; index++)
        {
            if (!probe.TryObserveDomainFactReservation(
                    CandidateKinds[index],
                    out failure))
            {
                return false;
            }
        }

        if (!inner.TryReserve(
                proposed,
                out object innerReservation,
                out failure))
        {
            return false;
        }
        if (innerReservation is not IDomainFactReservedIdentity identity)
        {
            inner.Cancel(innerReservation);
            failure = "combat-action-probe-reserved-identity-missing";
            return false;
        }

        reservation = new ProbeReservation(innerReservation, identity);
        return true;
    }

    public bool TryPrepare(
        in CombatActionFact actual,
        object reservation,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (reservation is not ProbeReservation state)
        {
            failure = "combat-action-probe-reservation-invalid";
            return false;
        }
        if (actual == null)
        {
            failure = "combat-action-probe-fact-missing";
            return false;
        }
        Observation[] observations = SelectObservations(actual);
        if (!inner.TryPrepare(
                actual,
                state.Inner,
                out IDomainFactPreparedPublication innerPrepared,
                out failure))
        {
            return false;
        }
        if (innerPrepared is not IDomainFactPreparedIdentity identity)
        {
            innerPrepared?.Cancel();
            failure = "combat-action-probe-prepared-identity-missing";
            return false;
        }

        prepared = new ProbePublication(
            commits,
            innerPrepared,
            observations,
            identity);
        return true;
    }

    public DomainFactDelivery Replay(in CombatActionFact fact) =>
        inner.Replay(fact);

    public bool TryPrepareReplay(
        string publicationKey,
        in CombatActionFact fact,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (fact == null)
        {
            failure = "combat-action-probe-replay-fact-missing";
            return false;
        }
        Observation[] observations = SelectObservations(fact);
        if (!inner.TryPrepareReplay(
                publicationKey,
                fact,
                out IDomainFactPreparedPublication innerPrepared,
                out failure))
        {
            return false;
        }
        if (innerPrepared is not IDomainFactPreparedIdentity identity)
        {
            innerPrepared?.Cancel();
            failure = "combat-action-probe-replay-identity-missing";
            return false;
        }

        prepared = new ProbePublication(
            commits,
            innerPrepared,
            observations,
            identity);
        return true;
    }

    public void Cancel(object reservation)
    {
        if (reservation is ProbeReservation state)
            inner.Cancel(state.Inner);
    }

    private static Observation[] SelectObservations(CombatActionFact fact)
    {
        GameplayResultKey[] related = CombatActionFactObserver.RelatedKeys(fact);
        var selected = new List<Observation>(CandidateKinds.Length);
        for (int kindIndex = 0; kindIndex < CandidateKinds.Length; kindIndex++)
        {
            MigratedProducerOutcomeKind kind = CandidateKinds[kindIndex];
            string producerId = MigratedProducerOutcomeCatalog.Get(kind).ProducerId;
            for (int keyIndex = 0; keyIndex < related.Length; keyIndex++)
            {
                if (string.Equals(
                        related[keyIndex].ProducerId,
                        producerId,
                        StringComparison.Ordinal))
                {
                    selected.Add(new Observation(kind, related[keyIndex]));
                    break;
                }
            }
        }
        return selected.ToArray();
    }

    private sealed class ProbeReservation : IDomainFactReservedIdentity
    {
        internal ProbeReservation(
            object inner,
            IDomainFactReservedIdentity identity)
        {
            Inner = inner;
            Identity = identity;
        }

        internal object Inner { get; }
        private IDomainFactReservedIdentity Identity { get; }
        public string PublicationKey => Identity.PublicationKey;
    }

    private readonly struct Observation
    {
        internal Observation(
            MigratedProducerOutcomeKind kind,
            GameplayResultKey key)
        {
            Kind = kind;
            Key = key;
        }

        internal MigratedProducerOutcomeKind Kind { get; }
        internal GameplayResultKey Key { get; }
    }

    private sealed class ProbePublication :
        IDomainFactPreparedPublication,
        IDomainFactPreparedIdentity
    {
        internal ProbePublication(
            ProbeCommitGroup commits,
            IDomainFactPreparedPublication inner,
            Observation[] observations,
            IDomainFactPreparedIdentity identity)
        {
            CommitGroup = commits;
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Observations = observations ?? throw new ArgumentNullException(nameof(observations));
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        }

        public IDomainFactCommitGroup CommitGroup { get; }
        internal IDomainFactPreparedPublication Inner { get; }
        internal Observation[] Observations { get; }
        private IDomainFactPreparedIdentity Identity { get; }
        public string OperationKey => Identity.OperationKey;
        public long OwnerRevision => Identity.OwnerRevision;
        public bool IsReplay => Identity.IsReplay;
        public void Cancel() => Inner.Cancel();
    }

    private sealed class ProbeCommitGroup : IDomainFactCommitGroup
    {
        private readonly GameplayOutcomeLedger ledger;
        private readonly MigratedProducerOutcomeProbeTransaction probe;

        internal ProbeCommitGroup(
            GameplayOutcomeLedger ledger,
            MigratedProducerOutcomeProbeTransaction probe)
        {
            this.ledger = ledger;
            this.probe = probe;
        }

        public bool Commit(
            IReadOnlyList<IDomainFactPreparedPublication> publications,
            DomainFactDelivery[] results,
            out string failure)
        {
            if (publications == null
                || results == null
                || publications.Count == 0
                || publications.Count != results.Length)
            {
                throw new ArgumentException(
                    "Invalid combat-action probe publication cohort.");
            }

            var values = new ProbePublication[publications.Count];
            IDomainFactCommitGroup innerGroup = null;
            bool reject = false;
            for (int index = 0; index < publications.Count; index++)
            {
                if (publications[index] is not ProbePublication value
                    || !ReferenceEquals(value.CommitGroup, this))
                {
                    throw new ArgumentException(
                        "Mismatched combat-action probe commit group.");
                }
                values[index] = value;
                innerGroup ??= value.Inner.CommitGroup;
                if (!ReferenceEquals(innerGroup, value.Inner.CommitGroup))
                    throw new ArgumentException("Combat-action inner commit groups differ.");
                if (value.IsReplay)
                    continue;
                for (int item = 0; item < value.Observations.Length; item++)
                    reject |= probe.ObserveDomainFactCommitAttempt(
                        value.Observations[item].Kind);
            }

            if (reject)
            {
                failure = "migrated-producer-e2e-injected-batch-commit-rejection";
                for (int index = 0; index < values.Length; index++)
                {
                    values[index].Cancel();
                    results[index] = DomainFactDelivery.Reject(failure);
                }
                return false;
            }

            var innerPublications = values
                .Select(value => value.Inner)
                .ToArray();
            bool committed = innerGroup.Commit(
                innerPublications,
                results,
                out failure);
            if (!committed)
                return false;

            for (int index = 0; index < values.Length; index++)
            {
                if (values[index].IsReplay)
                    continue;
                for (int item = 0; item < values[index].Observations.Length; item++)
                {
                    Observation observation = values[index].Observations[item];
                    if (ledger.TryGetResultIdentity(observation.Key, out _))
                    {
                        probe.ObserveDomainFactCommitted(
                            observation.Kind,
                            observation.Key);
                    }
                }
            }
            return true;
        }
    }
}
#endif
