using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

internal sealed class EmptyGameplayOutcomeNarrativeEvidenceQuery :
    IGameplayOutcomeNarrativeEvidenceQuery
{
    internal static EmptyGameplayOutcomeNarrativeEvidenceQuery Instance { get; } =
        new();

    private EmptyGameplayOutcomeNarrativeEvidenceQuery()
    {
    }

    public bool TryResolveExactBinding(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        out GameplayOutcomeNarrativeEvidenceSource source)
    {
        source = default;
        return false;
    }

    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> GetForEntity(
        GameplayEntityId entityId,
        int maximumCount,
        float minimumSalience,
        bool includeCompacted) =>
        Array.Empty<GameplayOutcomeNarrativeEvidenceSource>();

    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> GetForCharacter(
        string characterPersistentId,
        int maximumCount,
        float minimumSalience,
        bool includeCompacted) =>
        Array.Empty<GameplayOutcomeNarrativeEvidenceSource>();
}

internal sealed class EditorOffenseTruthFactFixture : IDisposable
{
    private readonly IDisposable observation;
    private readonly OffenseTruthPresentationObserver presentation;

    internal EditorOffenseTruthFactFixture(
        IGameEventBus events,
        string runId = "run:editor-offense-truth")
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new OffenseTruthRevealOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new OffenseTruthRevealOutcomeAdapter()
            });
        Ledger = new GameplayOutcomeLedger(
            registry,
            new GameplayOutcomeBufferLimits(),
            new GameplayOutcomeRunId(runId),
            1L);
        GameplayOutcomeRecorder recorder = new(Ledger, registry, events);
        Facts = new DomainFactDispatcher();
        observation = Connect(registry, recorder);
        presentation = new OffenseTruthPresentationObserver(events);
        presentation.Initialize();
    }

    internal EditorOffenseTruthFactFixture(
        IGameEventBus events,
        GameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        GameplayOutcomeRecorder recorder)
    {
        _ = events ?? throw new ArgumentNullException(nameof(events));
        Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        Facts = new DomainFactDispatcher();
        observation = Connect(
            registry ?? throw new ArgumentNullException(nameof(registry)),
            recorder ?? throw new ArgumentNullException(nameof(recorder)));
        presentation = new OffenseTruthPresentationObserver(events);
        presentation.Initialize();
    }

    internal DomainFactDispatcher Facts { get; }
    internal GameplayOutcomeLedger Ledger { get; }

    private IDisposable Connect(
        GameplayOutcomeRegistry registry,
        GameplayOutcomeRecorder recorder) => Facts.Subscribe(
        "log:offense.truth-reveal",
        new GameplayDomainFactObserver<
            OffenseTruthRevealedFact,
            OffenseTruthRevealOutcomeReceipt>(
            OffenseTruthRevealFactMapping.Capture,
            registry,
            Ledger,
            recorder,
            new GameplayDomainFactCommitGroup(recorder, Ledger)));

    public void Dispose()
    {
        presentation.Dispose();
        observation.Dispose();
        Facts.Dispose();
    }
}

internal sealed class EditorOffenseExpeditionResultFactFixture : IDisposable
{
    private const MigratedProducerOutcomeKind OutcomeKind =
        MigratedProducerOutcomeKind.OffenseExpeditionResult;

    private readonly IDisposable resultObservation;
    private readonly IDisposable stockObservation;
    private readonly OffenseExpeditionResultPresentationObserver presentation;

    internal EditorOffenseExpeditionResultFactFixture(
        IGameEventBus events,
        MigratedProducerOutcomeRuntime migrated,
        GameplayOutcomeRecorder recorder,
        GameplayOutcomeLedger ledger,
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        _ = events ?? throw new ArgumentNullException(nameof(events));
        _ = migrated ?? throw new ArgumentNullException(nameof(migrated));
        _ = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _ = ledger ?? throw new ArgumentNullException(nameof(ledger));

        Facts = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
        var observer = new OffenseExpeditionResultFactObserver(
            migrated,
            commits);
        try
        {
            resultObservation = probe == null
                ? observer.Connect(Facts)
                : Facts.Subscribe(
                    "log:offense.expedition-result",
                    new ProbeObserver(observer, commits, probe));
            stockObservation = probe == null
                ? Facts.Subscribe(
                    "log:offense.stock-reward",
                    new MigratedDomainFactObserver<
                        OffenseStockRewardCompletedFact>(
                        OffenseStockRewardFactMapping.Capture,
                        migrated,
                        commits))
                : Facts.Subscribe(
                    "log:offense.stock-reward",
                    new MigratedProducerDomainFactProbeObserver<
                        OffenseStockRewardCompletedFact>(
                        OffenseStockRewardFactMapping.Capture,
                        migrated,
                        commits,
                        probe));
            Publications = new OffenseExpeditionResultPublicationRuntime(
                Facts,
                Facts,
                events);
            presentation = new OffenseExpeditionResultPresentationObserver(
                events,
                new CharacterIdentityEventPublisher(events));
            presentation.Initialize();
        }
        catch
        {
            presentation?.Dispose();
            stockObservation?.Dispose();
            resultObservation?.Dispose();
            Facts.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher Facts { get; }
    internal OffenseExpeditionResultPublicationRuntime Publications { get; }

    internal void FlushNotifications()
    {
        if (!Publications.RecoverPending(out string failure))
        {
            throw new InvalidOperationException(
                "Offense expedition result fixture could not recover its canonical publication: "
                + failure);
        }
    }

    public void Dispose()
    {
        presentation.Dispose();
        stockObservation.Dispose();
        resultObservation.Dispose();
        Facts.Dispose();
    }

    private sealed class ProbeObserver :
        IDurableDomainFactObserver<OffenseExpeditionResolvedFact>,
        IDurableDomainFactRecoveryObserver<OffenseExpeditionResolvedFact>
    {
        private readonly OffenseExpeditionResultFactObserver inner;
        private readonly MigratedProducerDomainFactProbeCommitGroup commits;
        private readonly MigratedProducerOutcomeProbeTransaction probe;

        internal ProbeObserver(
            OffenseExpeditionResultFactObserver inner,
            GameplayDomainFactCommitGroup commits,
            MigratedProducerOutcomeProbeTransaction probe)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
            this.commits = MigratedProducerDomainFactProbeCommitGroupCache.Get(
                commits ?? throw new ArgumentNullException(nameof(commits)),
                probe);
        }

        public bool TryReserve(
            in OffenseExpeditionResolvedFact fact,
            out object reservation,
            out string failure)
        {
            reservation = null;
            if (!probe.TryObserveDomainFactReservation(
                    OutcomeKind,
                    out failure))
            {
                return false;
            }
            if (!inner.TryReserve(fact, out object innerReservation, out failure))
                return false;
            if (innerReservation is not IDomainFactReservedIdentity identity)
            {
                inner.Cancel(innerReservation);
                failure =
                    "offense-expedition-result-probe-reserved-identity-missing";
                return false;
            }
            reservation = new ProbeReservation(innerReservation, identity);
            return true;
        }

        public bool TryPrepare(
            in OffenseExpeditionResolvedFact fact,
            object reservation,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            if (reservation is not ProbeReservation state)
            {
                failure = "offense-expedition-result-probe-reservation-invalid";
                return false;
            }
            if (!inner.TryPrepare(
                    fact,
                    state.Inner,
                    out IDomainFactPreparedPublication innerPrepared,
                    out failure))
            {
                return false;
            }
            if (innerPrepared is not GameplayDomainFactPublication publication)
            {
                innerPrepared?.Cancel();
                failure = "offense-expedition-result-probe-publication-invalid";
                return false;
            }
            prepared = new MigratedProducerDomainFactProbePublication(
                commits,
                publication,
                OutcomeKind);
            return true;
        }

        public DomainFactDelivery Replay(
            in OffenseExpeditionResolvedFact fact) => inner.Replay(fact);

        public bool TryPrepareReplay(
            string publicationKey,
            in OffenseExpeditionResolvedFact fact,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            if (!inner.TryPrepareReplay(
                    publicationKey,
                    fact,
                    out IDomainFactPreparedPublication innerPrepared,
                    out failure))
            {
                return false;
            }
            if (innerPrepared is not GameplayDomainFactPublication publication)
            {
                innerPrepared?.Cancel();
                failure =
                    "offense-expedition-result-probe-recovery-publication-invalid";
                return false;
            }
            prepared = new MigratedProducerDomainFactProbePublication(
                commits,
                publication,
                OutcomeKind);
            return true;
        }

        public void Cancel(object reservation)
        {
            if (reservation is ProbeReservation state)
                inner.Cancel(state.Inner);
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
    }
}

internal sealed class EditorFacilityUseFactFixture : IDisposable
{
    private readonly IDisposable observation;

    internal EditorFacilityUseFactFixture(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe = null)
        : this(
            outcomes?.Transaction
                ?? throw new ArgumentNullException(nameof(outcomes)),
            outcomes.Recorder,
            outcomes.Ledger,
            probe)
    {
    }

    internal EditorFacilityUseFactFixture(
        MigratedProducerOutcomeRuntime migrated,
        GameplayOutcomeRecorder recorder,
        GameplayOutcomeLedger ledger,
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        migrated = migrated ?? throw new ArgumentNullException(nameof(migrated));
        recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

        Facts = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
        try
        {
            observation = probe == null
                ? Facts.Subscribe(
                    "log:facility.use-completed",
                    new MigratedDomainFactObserver<FacilityUseCompletedFact>(
                        FacilityUseFactMapping.Capture,
                        migrated,
                        commits))
                : Facts.Subscribe(
                    "log:facility.use-completed",
                    new MigratedProducerDomainFactProbeObserver<
                        FacilityUseCompletedFact>(
                        FacilityUseFactMapping.Capture,
                        migrated,
                        commits,
                        probe));
        }
        catch
        {
            observation?.Dispose();
            Facts.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher Facts { get; }

    public void Dispose()
    {
        observation?.Dispose();
        Facts.Dispose();
    }
}

internal sealed class EditorOffenseArrivalSettlementFactFixture : IDisposable
{
    private const MigratedProducerOutcomeKind OutcomeKind =
        MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt;

    private readonly IDisposable observation;
    private readonly OffenseArrivalPresentationObserver presentation;

    internal EditorOffenseArrivalSettlementFactFixture(
        IGameEventBus events,
        MigratedProducerOutcomeRuntime migrated,
        GameplayOutcomeRecorder recorder,
        GameplayOutcomeLedger ledger,
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        _ = events ?? throw new ArgumentNullException(nameof(events));
        _ = migrated ?? throw new ArgumentNullException(nameof(migrated));
        _ = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _ = ledger ?? throw new ArgumentNullException(nameof(ledger));

        Facts = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
        var observer = new OffenseArrivalSettlementFactObserver(
            migrated,
            commits);
        try
        {
            observation = probe == null
                ? observer.Connect(Facts)
                : Facts.Subscribe(
                    "log:offense.arrival-settled",
                    new ProbeObserver(observer, commits, probe));
            Settlements = new OffenseArrivalSettlementRuntime(Facts, Facts);
            presentation = new OffenseArrivalPresentationObserver(events);
            presentation.Initialize();
        }
        catch
        {
            presentation?.Dispose();
            observation?.Dispose();
            Facts.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher Facts { get; }
    internal OffenseArrivalSettlementRuntime Settlements { get; }

    public void Dispose()
    {
        presentation.Dispose();
        observation.Dispose();
        Facts.Dispose();
    }

    private sealed class ProbeObserver :
        IDurableDomainFactObserver<OffenseArrivalSettledFact>,
        IDurableDomainFactRecoveryObserver<OffenseArrivalSettledFact>
    {
        private readonly OffenseArrivalSettlementFactObserver inner;
        private readonly MigratedProducerDomainFactProbeCommitGroup commits;
        private readonly MigratedProducerOutcomeProbeTransaction probe;

        internal ProbeObserver(
            OffenseArrivalSettlementFactObserver inner,
            GameplayDomainFactCommitGroup commits,
            MigratedProducerOutcomeProbeTransaction probe)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
            this.commits = MigratedProducerDomainFactProbeCommitGroupCache.Get(
                commits ?? throw new ArgumentNullException(nameof(commits)),
                probe);
        }

        public bool TryReserve(
            in OffenseArrivalSettledFact fact,
            out object reservation,
            out string failure)
        {
            reservation = null;
            if (!probe.TryObserveDomainFactReservation(
                    OutcomeKind,
                    out failure))
            {
                return false;
            }
            if (!inner.TryReserve(fact, out object innerReservation, out failure))
                return false;
            if (innerReservation is not IDomainFactReservedIdentity identity)
            {
                inner.Cancel(innerReservation);
                failure =
                    "offense-arrival-settlement-probe-reserved-identity-missing";
                return false;
            }
            reservation = new ProbeReservation(innerReservation, identity);
            return true;
        }

        public bool TryPrepare(
            in OffenseArrivalSettledFact fact,
            object reservation,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            if (reservation is not ProbeReservation state)
            {
                failure = "offense-arrival-settlement-probe-reservation-invalid";
                return false;
            }
            if (!inner.TryPrepare(
                    fact,
                    state.Inner,
                    out IDomainFactPreparedPublication innerPrepared,
                    out failure))
            {
                return false;
            }
            if (innerPrepared is not GameplayDomainFactPublication publication)
            {
                innerPrepared?.Cancel();
                failure =
                    "offense-arrival-settlement-probe-publication-invalid";
                return false;
            }
            prepared = new MigratedProducerDomainFactProbePublication(
                commits,
                publication,
                OutcomeKind);
            return true;
        }

        public DomainFactDelivery Replay(
            in OffenseArrivalSettledFact fact) => inner.Replay(fact);

        public bool TryPrepareReplay(
            string publicationKey,
            in OffenseArrivalSettledFact fact,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            if (!inner.TryPrepareReplay(
                    publicationKey,
                    fact,
                    out IDomainFactPreparedPublication innerPrepared,
                    out failure))
            {
                return false;
            }
            if (innerPrepared is not GameplayDomainFactPublication publication)
            {
                innerPrepared?.Cancel();
                failure =
                    "offense-arrival-settlement-probe-recovery-publication-invalid";
                return false;
            }
            prepared = new MigratedProducerDomainFactProbePublication(
                commits,
                publication,
                OutcomeKind);
            return true;
        }

        public void Cancel(object reservation)
        {
            if (reservation is ProbeReservation state)
                inner.Cancel(state.Inner);
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
    }
}

internal sealed class EditorOffenseNodeFactFixture : IDisposable
{
    private const MigratedProducerOutcomeKind OutcomeKind =
        MigratedProducerOutcomeKind.OffenseExpeditionNodeResult;

    private readonly IDisposable observation;
    private readonly OffenseNodeExperienceObserver experience;

    internal EditorOffenseNodeFactFixture(
        IGameEventBus events,
        MigratedProducerOutcomeRuntime migrated,
        GameplayOutcomeRecorder recorder,
        GameplayOutcomeLedger ledger,
        MigratedProducerOutcomeProbeTransaction probe = null,
        ICharacterLifetimeQuery characters = null)
    {
        _ = events ?? throw new ArgumentNullException(nameof(events));
        _ = migrated ?? throw new ArgumentNullException(nameof(migrated));
        _ = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _ = ledger ?? throw new ArgumentNullException(nameof(ledger));

        Facts = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
        var observer = new OffenseNodeFactObserver(migrated, commits);
        try
        {
            observation = probe == null
                ? observer.Connect(Facts)
                : Facts.Subscribe(
                    "log:offense.node-resolved",
                    new ProbeObserver(observer, commits, probe));
            Publications = new OffenseNodePublicationRuntime(
                Facts,
                Facts,
                events);
            if (characters != null)
            {
                experience = new OffenseNodeExperienceObserver(
                    events,
                    characters);
                experience.Initialize();
            }
        }
        catch
        {
            experience?.Dispose();
            observation?.Dispose();
            Facts.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher Facts { get; }
    internal OffenseNodePublicationRuntime Publications { get; }

    public void Dispose()
    {
        experience?.Dispose();
        observation.Dispose();
        Facts.Dispose();
    }

    private sealed class ProbeObserver :
        IDurableDomainFactObserver<OffenseNodeResolutionFact>,
        IDurableDomainFactRecoveryObserver<OffenseNodeResolutionFact>
    {
        private readonly OffenseNodeFactObserver inner;
        private readonly MigratedProducerDomainFactProbeCommitGroup commits;
        private readonly MigratedProducerOutcomeProbeTransaction probe;

        internal ProbeObserver(
            OffenseNodeFactObserver inner,
            GameplayDomainFactCommitGroup commits,
            MigratedProducerOutcomeProbeTransaction probe)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
            this.commits = MigratedProducerDomainFactProbeCommitGroupCache.Get(
                commits ?? throw new ArgumentNullException(nameof(commits)),
                probe);
        }

        public bool TryReserve(
            in OffenseNodeResolutionFact fact,
            out object reservation,
            out string failure)
        {
            reservation = null;
            if (!probe.TryObserveDomainFactReservation(
                    OutcomeKind,
                    out failure))
            {
                return false;
            }
            if (!inner.TryReserve(fact, out object innerReservation, out failure))
                return false;
            if (innerReservation is not IDomainFactReservedIdentity identity)
            {
                inner.Cancel(innerReservation);
                failure = "offense-node-probe-reserved-identity-missing";
                return false;
            }
            reservation = new ProbeReservation(innerReservation, identity);
            return true;
        }

        public bool TryPrepare(
            in OffenseNodeResolutionFact fact,
            object reservation,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            if (reservation is not ProbeReservation state)
            {
                failure = "offense-node-probe-reservation-invalid";
                return false;
            }
            if (!inner.TryPrepare(
                    fact,
                    state.Inner,
                    out IDomainFactPreparedPublication innerPrepared,
                    out failure))
            {
                return false;
            }
            if (innerPrepared is not GameplayDomainFactPublication publication)
            {
                innerPrepared?.Cancel();
                failure = "offense-node-probe-publication-invalid";
                return false;
            }
            prepared = new MigratedProducerDomainFactProbePublication(
                commits,
                publication,
                OutcomeKind);
            return true;
        }

        public DomainFactDelivery Replay(
            in OffenseNodeResolutionFact fact) => inner.Replay(fact);

        public bool TryPrepareReplay(
            string publicationKey,
            in OffenseNodeResolutionFact fact,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            if (!inner.TryPrepareReplay(
                    publicationKey,
                    fact,
                    out IDomainFactPreparedPublication innerPrepared,
                    out failure))
            {
                return false;
            }
            if (innerPrepared is not GameplayDomainFactPublication publication)
            {
                innerPrepared?.Cancel();
                failure = "offense-node-probe-recovery-publication-invalid";
                return false;
            }
            prepared = new MigratedProducerDomainFactProbePublication(
                commits,
                publication,
                OutcomeKind);
            return true;
        }

        public void Cancel(object reservation)
        {
            if (reservation is ProbeReservation state)
                inner.Cancel(state.Inner);
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
    }
}

internal sealed class EditorFixedGameCalendar : IGameCalendar
{
    internal static EditorFixedGameCalendar Instance { get; } = new();

    internal EditorFixedGameCalendar(int day = 1, int hour = 0)
    {
        SetDateTime(day, hour);
    }

    public int Day { get; private set; } = 1;
    public int Hour { get; private set; }
    public int Year => Current.Year;
    public int DayOfYear => Current.DayOfYear;
    public Season Season => Current.Season;
    public int DayOfSeason => Current.DayOfSeason;
    public long AbsoluteHour => Current.AbsoluteHour;
    public float ElapsedSeconds => 0f;
    public TimeOfDay TimeOfDay => default;
    public bool IsRunning { get; private set; }
    public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);

    public CalendarDateTime GetRegionalTime(int utcOffsetHours) => GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);

    public void Start() => IsRunning = true;

    public void SetDateTime(int day, int hour)
    {
        if (day < 1 || hour < 0 || hour >= GameCalendarRules.HoursPerDay)
            throw new ArgumentOutOfRangeException(nameof(day), "Invalid fixture calendar date.");
        Day = day;
        Hour = hour;
    }
}

internal sealed class EditorNoOpEnvironmentOutcomeCommitter :
    IEnvironmentGameplayOutcomeCommitter
{
    internal static EditorNoOpEnvironmentOutcomeCommitter Instance { get; } =
        new();

    private EditorNoOpEnvironmentOutcomeCommitter()
    {
    }

    public bool TryReserve(
        in EnvironmentOutcomeReservationSpec spec,
        out ReservedEnvironmentOutcome reserved,
        out string failureReason)
    {
        reserved = default;
        failureReason = "editor-noop-environment-outcome";
        return false;
    }

    public bool TryWriteReserved<TReceipt>(
        in TReceipt receipt,
        in ReservedEnvironmentOutcome reserved,
        out PreparedEnvironmentOutcome prepared,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt
    {
        prepared = default;
        failureReason = "editor-noop-environment-outcome";
        return false;
    }

    public bool TryPrepare<TReceipt>(
        in TReceipt receipt,
        out PreparedEnvironmentOutcome prepared,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt
    {
        prepared = default;
        failureReason = "editor-noop-environment-outcome";
        return false;
    }

    public EnvironmentOutcomeCommitResult Commit(
        in PreparedEnvironmentOutcome prepared,
        long expectedOwnerRevision) => default;

    public EnvironmentOutcomeCommitResult Reconcile(GameplayResultKey resultKey) =>
        default;

    public bool IsCanonicalAcknowledgedReplay<TReceipt>(
        in TReceipt receipt,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt
    {
        failureReason = "editor-noop-environment-outcome";
        return false;
    }

    public void Cancel(in PreparedEnvironmentOutcome prepared)
    {
    }

    public void Cancel(in ReservedEnvironmentOutcome reserved)
    {
    }
}
