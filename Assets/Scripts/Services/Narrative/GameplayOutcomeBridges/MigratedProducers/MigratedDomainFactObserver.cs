using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;
using VContainer;

// This mapping is owned by observers. Gameplay producers never construct it.
public readonly struct MigratedDomainFactCapture
{
    private readonly Func<GameplayResultKey, MigratedProducerOutcomeDetails> detailsFactory;
    public MigratedDomainFactCapture(MigratedProducerOutcomeKind kind, string ownerIdentity,
        int absoluteDay, GameplayOutcomeStatus status, MigratedProducerOutcomeDetails details,
        bool reservationOnly = false)
    { Kind = kind; OwnerIdentity = ownerIdentity; AbsoluteDay = absoluteDay; Status = status; Details = details; detailsFactory = null; ReservationOnly = reservationOnly; }
    public MigratedDomainFactCapture(MigratedProducerOutcomeKind kind, string ownerIdentity,
        int absoluteDay, GameplayOutcomeStatus status,
        Func<GameplayResultKey, MigratedProducerOutcomeDetails> detailsFactory, bool reservationOnly = false)
    {
        Kind = kind; OwnerIdentity = ownerIdentity; AbsoluteDay = absoluteDay; Status = status;
        Details = default;
        ReservationOnly = reservationOnly;
        this.detailsFactory = detailsFactory ?? throw new ArgumentNullException(nameof(detailsFactory));
    }
    public MigratedProducerOutcomeKind Kind { get; }
    public string OwnerIdentity { get; }
    public int AbsoluteDay { get; }
    public GameplayOutcomeStatus Status { get; }
    public MigratedProducerOutcomeDetails Details { get; }
    public bool ReservationOnly { get; }
    internal MigratedProducerOutcomeDetails ResolveDetails(GameplayResultKey key) =>
        detailsFactory == null ? Details : detailsFactory(key);
}

public sealed class MigratedDomainFactObserver<TFact> : IDurableDomainFactObserver<TFact>,
    IDurableDomainFactRecoveryObserver<TFact>
{
    private readonly Func<TFact, MigratedDomainFactCapture> capture;
    private readonly MigratedProducerOutcomeRuntime runtime;
    private readonly GameplayDomainFactCommitGroup commits;
    public MigratedDomainFactObserver(Func<TFact, MigratedDomainFactCapture> capture,
        MigratedProducerOutcomeRuntime runtime, GameplayDomainFactCommitGroup commits)
    {
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.commits = commits ?? throw new ArgumentNullException(nameof(commits));
    }
    public bool TryReserve(in TFact proposed, out object reservation, out string failure)
    {
        reservation = null;
        var value = capture(proposed);
        if (!runtime.TryReserveObserved(value, out var prepared, out failure)) return false;
        reservation = new Reservation(value, prepared);
        return true;
    }
    public bool TryPrepare(in TFact actual, object reservation,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        if (reservation is not Reservation state || state.Publication != null)
        { failure = "fact-observer-migrated-reservation-invalid"; return false; }
        var value = capture(actual);
        if (value.ReservationOnly)
        { failure = "fact-observer-proposal-cannot-be-published"; return false; }
        if (value.Kind != state.Capture.Kind || value.OwnerIdentity != state.Capture.OwnerIdentity
            || value.AbsoluteDay != state.Capture.AbsoluteDay || value.Status != state.Capture.Status)
        { failure = "fact-observer-migrated-reserved-identity-changed"; return false; }
        if (!runtime.TryPrepareObserved(state.Prepared, value.ResolveDetails(state.Prepared.ResultKey), commits,
                out var publication, out failure)) return false;
        state.Publication = publication;
        prepared = publication;
        return true;
    }
    public DomainFactDelivery Replay(in TFact fact) => DomainFactDelivery.Reject(
        "fact-observer-replay-requires-saved-operation-identity:" + typeof(TFact).Name);
    public bool TryPrepareReplay(string publicationKey, in TFact fact,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        bool accepted = runtime.TryPrepareObservedRecovery(capture(fact), publicationKey,
            commits, out var publication, out failure);
        prepared = publication;
        return accepted;
    }
    public void Cancel(object reservation)
    {
        if (reservation is not Reservation state) return;
        state.Publication?.Cancel();
        runtime.Cancel(state.Prepared);
    }
    private sealed class Reservation : IDomainFactReservedIdentity
    {
        public Reservation(MigratedDomainFactCapture capture, PreparedMigratedProducerOutcome prepared)
        { Capture = capture; Prepared = prepared; }
        public MigratedDomainFactCapture Capture { get; }
        public PreparedMigratedProducerOutcome Prepared { get; }
        public GameplayDomainFactPublication Publication;
        public string PublicationKey => Prepared.ResultKey.ToString();
    }
}

public sealed class MigratedDomainFactObserverBinding<TFact> : IDomainFactObserverBinding
{
    private readonly string observerId;
    private readonly MigratedDomainFactObserver<TFact> observer;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(TFact) });
    public MigratedDomainFactObserverBinding(string observerId, Func<TFact, MigratedDomainFactCapture> capture,
        MigratedProducerOutcomeRuntime runtime, GameplayDomainFactCommitGroup commits)
    { this.observerId = observerId; observer = new MigratedDomainFactObserver<TFact>(capture, runtime, commits); }
    public IDisposable Connect(DomainFactDispatcher dispatcher) => dispatcher.Subscribe(observerId, observer);
}

public static class MigratedDomainFactRegistration
{
    public static void RegisterMigratedDomainFact<TFact>(this IContainerBuilder builder,
        string observerId, Func<TFact, MigratedDomainFactCapture> capture)
    {
        builder.Register(resolver => new MigratedDomainFactObserverBinding<TFact>(observerId, capture,
            resolver.Resolve<MigratedProducerOutcomeRuntime>(),
            resolver.Resolve<GameplayDomainFactCommitGroup>()), Lifetime.Singleton).As<IDomainFactObserverBinding>();
    }
}
