using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Owns the legacy triggered/state-changed narrative pair for one defense
/// activation. The gameplay owner publishes only the neutral frozen fact.
/// </summary>
public sealed class DefenseActivationFactObserver :
    IDurableDomainFactObserver<DefenseActivationFact>,
    IDurableDomainFactRecoveryObserver<DefenseActivationFact>,
    IDomainFactCommitGroup,
    IDomainFactObserverBinding
{
    public const string ObserverId = "log:defense.facility-activation";

    private readonly MigratedProducerOutcomeRuntime outcomes;
    private readonly GameplayDomainFactCommitGroup commits;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(DefenseActivationFact) });

    public DefenseActivationFactObserver(
        MigratedProducerOutcomeRuntime outcomes,
        GameplayDomainFactCommitGroup commits)
    {
        this.outcomes = outcomes
            ?? throw new ArgumentNullException(nameof(outcomes));
        this.commits = commits
            ?? throw new ArgumentNullException(nameof(commits));
    }

    public IDisposable Connect(DomainFactDispatcher dispatcher) =>
        dispatcher.Subscribe(ObserverId, this);

    public static MigratedDomainFactCapture Capture(
        DefenseActivationFact fact,
        bool state)
    {
        DefenseActivationFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        DefenseActivationFactData data = value.Snapshot();
        data.Validate();

        var subject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.FacilityKind,
            data.facilityId,
            data.facilityName,
            MigratedProducerOutcomeIds.FacilityRole);
        string summary = state
            ? "state=" + data.afterState
                + ";condition=" + data.conditionAfter.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + ";supply=" + data.supplyAfter
                + ";activationCount=" + data.activationCount
                + ";blocked=" + data.blockedReason
            : "facility=" + data.facilityId
                + ";target=" + data.targetId
                + ";timing=" + data.timing
                + ";concept=" + data.concept
                + ";jammed=" + data.jammed
                + ";misfired=" + data.misfired;
        var details = new MigratedProducerOutcomeDetails(
            new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            summary);
        return new MigratedDomainFactCapture(
            state
                ? MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent
                : MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent,
            data.facilityId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details,
            reservationOnly: value.IsProposal);
    }

    public bool TryReserve(
        in DefenseActivationFact proposal,
        out object reservation,
        out string failure)
    {
        reservation = null;
        if (proposal == null || !proposal.IsProposal)
        {
            failure = "defense-activation-proposal-required";
            return false;
        }

        DefenseActivationFactData data = proposal.Snapshot();
        if (!outcomes.TryReserveObserved(
                Capture(proposal, state: false),
                out PreparedMigratedProducerOutcome triggered,
                out failure))
        {
            return false;
        }

        PreparedMigratedProducerOutcome changed = default;
        try
        {
            if (triggered.OwnerRevision != data.activationCount
                || triggered.ResultKey != ExpectedKey(
                    MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent,
                    data))
            {
                outcomes.Cancel(triggered);
                failure = "defense-activation-trigger-revision-conflict";
                return false;
            }

            if (!outcomes.TryReserveObserved(
                    Capture(proposal, state: true),
                    out changed,
                    out failure))
            {
                outcomes.Cancel(triggered);
                return false;
            }

            if (changed.OwnerRevision != data.activationCount
                || changed.ResultKey != ExpectedKey(
                    MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent,
                    data))
            {
                outcomes.Cancel(triggered);
                outcomes.Cancel(changed);
                failure = "defense-activation-state-revision-conflict";
                return false;
            }

            reservation = new Reservation(proposal, triggered, changed);
            failure = string.Empty;
            return true;
        }
        catch
        {
            outcomes.Cancel(triggered);
            if (changed.IsValid)
                outcomes.Cancel(changed);
            throw;
        }
    }

    public bool TryPrepare(
        in DefenseActivationFact actual,
        object reservation,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (reservation is not Reservation state
            || state.Used
            || actual == null
            || actual.IsProposal
            || !actual.MatchesProposal(state.Proposal))
        {
            failure = "defense-activation-reserved-source-changed";
            return false;
        }

        MigratedDomainFactCapture triggerCapture = Capture(actual, state: false);
        if (!outcomes.TryPrepareObserved(
                state.Triggered,
                triggerCapture.ResolveDetails(state.Triggered.ResultKey),
                commits,
                out GameplayDomainFactPublication triggered,
                out failure))
        {
            return false;
        }

        GameplayDomainFactPublication changed = null;
        try
        {
            MigratedDomainFactCapture stateCapture = Capture(actual, state: true);
            if (!outcomes.TryPrepareObserved(
                    state.Changed,
                    stateCapture.ResolveDetails(state.Changed.ResultKey),
                    commits,
                    out changed,
                    out failure))
            {
                triggered.Cancel();
                return false;
            }

            state.Used = true;
            prepared = state.Publication = new Publication(
                this,
                triggered,
                changed);
            return true;
        }
        catch
        {
            triggered.Cancel();
            changed?.Cancel();
            throw;
        }
    }

    public bool TryPrepareReplay(
        string publicationKey,
        in DefenseActivationFact actual,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (actual == null || actual.IsProposal)
        {
            failure = "defense-activation-actual-required";
            return false;
        }
        if (!GameplayDomainFactPairProof.TryReadKeys(
                publicationKey,
                out string triggerKey,
                out string stateKey))
        {
            failure = "defense-activation-recovery-cohort-shape";
            return false;
        }

        DefenseActivationFactData data = actual.Snapshot();
        if (triggerKey != ExpectedKey(
                    MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent,
                    data).ToString()
            || stateKey != ExpectedKey(
                    MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent,
                    data).ToString())
        {
            failure = "defense-activation-recovery-reference-conflict";
            return false;
        }

        if (!outcomes.TryPrepareObservedRecovery(
                Capture(actual, state: false),
                triggerKey,
                commits,
                out GameplayDomainFactPublication triggered,
                out failure))
        {
            return false;
        }

        GameplayDomainFactPublication changed = null;
        try
        {
            if (!outcomes.TryPrepareObservedRecovery(
                    Capture(actual, state: true),
                    stateKey,
                    commits,
                    out changed,
                    out failure))
            {
                triggered.Cancel();
                return false;
            }

            var publication = new Publication(this, triggered, changed);
            if (publication.OperationKey != publicationKey
                || triggered.IsReplay != changed.IsReplay)
            {
                publication.Cancel();
                failure = "defense-activation-recovery-torn-pair";
                return false;
            }

            prepared = publication;
            failure = string.Empty;
            return true;
        }
        catch
        {
            triggered.Cancel();
            changed?.Cancel();
            throw;
        }
    }

    public bool Commit(
        IReadOnlyList<IDomainFactPreparedPublication> publications,
        DomainFactDelivery[] results,
        out string failure)
    {
        if (publications?.Count != 1
            || results?.Length != 1
            || publications[0] is not Publication value
            || value.CommitGroup != this
            || value.Used)
        {
            throw new ArgumentException(
                "defense-activation-publication-cohort-invalid");
        }

        value.Used = true;
        var children = new IDomainFactPreparedPublication[]
        {
            value.Triggered,
            value.Changed
        };
        var deliveries = new DomainFactDelivery[2];
        bool accepted = commits.Commit(children, deliveries, out failure);
        results[0] = GameplayDomainFactPairProof.Combine(
            value.OperationKey,
            deliveries[0],
            deliveries[1]);
        return accepted;
    }

    public DomainFactDelivery Replay(in DefenseActivationFact actual) =>
        DomainFactDelivery.Reject(
            "defense-activation-replay-requires-saved-reference");

    public void Cancel(object reservation)
    {
        if (reservation is not Reservation state)
            return;
        state.Used = true;
        state.Publication?.Cancel();
        outcomes.Cancel(state.Triggered);
        outcomes.Cancel(state.Changed);
    }

    private static GameplayResultKey ExpectedKey(
        MigratedProducerOutcomeKind kind,
        DefenseActivationFactData data) =>
        MigratedProducerOutcomeCatalog.ResultKey(
            kind,
            data.facilityId,
            data.activationCount);

    private sealed class Reservation : IDomainFactReservedIdentity
    {
        internal Reservation(
            DefenseActivationFact proposal,
            PreparedMigratedProducerOutcome triggered,
            PreparedMigratedProducerOutcome changed)
        {
            Proposal = proposal;
            Triggered = triggered;
            Changed = changed;
            PublicationKey = GameplayDomainFactPairProof.Key(
                triggered.ResultKey.ToString(),
                changed.ResultKey.ToString());
        }

        internal readonly DefenseActivationFact Proposal;
        internal readonly PreparedMigratedProducerOutcome Triggered;
        internal readonly PreparedMigratedProducerOutcome Changed;
        internal Publication Publication;
        internal bool Used;
        public string PublicationKey { get; }
    }

    private sealed class Publication :
        IDomainFactPreparedPublication,
        IDomainFactPreparedIdentity
    {
        internal Publication(
            DefenseActivationFactObserver owner,
            GameplayDomainFactPublication triggered,
            GameplayDomainFactPublication changed)
        {
            CommitGroup = owner;
            Triggered = triggered;
            Changed = changed;
            OperationKey = GameplayDomainFactPairProof.Key(
                triggered.OperationKey,
                changed.OperationKey);
        }

        internal readonly GameplayDomainFactPublication Triggered;
        internal readonly GameplayDomainFactPublication Changed;
        internal bool Used;
        public IDomainFactCommitGroup CommitGroup { get; }
        public string OperationKey { get; }
        public long OwnerRevision => Triggered.OwnerRevision;
        public bool IsReplay => Triggered.IsReplay && Changed.IsReplay;

        public void Cancel()
        {
            if (Used)
                return;
            Used = true;
            Triggered.Cancel();
            Changed.Cancel();
        }
    }
}
