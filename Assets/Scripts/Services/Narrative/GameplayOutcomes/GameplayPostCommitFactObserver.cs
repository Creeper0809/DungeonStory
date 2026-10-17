using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Observes an already-applied one-shot auxiliary notification. It is not a ledger,
/// does not reapply the producing mutation, is not persisted, and cannot be replayed.
/// </summary>
public sealed class GameplayPostCommitFactObserver<TFact> :
    IDurableDomainFactObserver<TFact>, IDomainFactCommitGroup
{
    private readonly Func<TFact, string> validate;
    private readonly Action<TFact> notify;
    public GameplayPostCommitFactObserver(Func<TFact, string> validate, Action<TFact> notify)
    { this.validate = validate ?? throw new ArgumentNullException(nameof(validate)); this.notify = notify; }
    public bool TryReserve(in TFact proposed, out object reservation, out string failure)
    { reservation = new Reservation(validate(proposed)); failure = string.Empty; return true; }
    public bool TryPrepare(in TFact actual, object reservation, out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        if (reservation is not Reservation state || state.Used || validate(actual) != state.PublicationKey)
        { failure = "post-commit-observation-reservation-invalid"; return false; }
        state.Used = true;
        prepared = new Publication(this, state.PublicationKey, actual);
        failure = string.Empty;
        return true;
    }
    public void Cancel(object reservation) { if (reservation is Reservation state) state.Used = true; }
    public DomainFactDelivery Replay(in TFact fact) => DomainFactDelivery.Reject("post-commit-observation-replay-unsupported");
    public bool Commit(IReadOnlyList<IDomainFactPreparedPublication> publications, DomainFactDelivery[] results, out string failure)
    {
        if (publications?.Count != 1 || results?.Length != 1 || publications[0] is not Publication value
            || value.CommitGroup != this || value.Used)
        { failure = "post-commit-observation-publication-invalid"; return false; }
        value.Used = true;
        notify?.Invoke(value.Fact);
        results[0] = new DomainFactDelivery(DomainFactDeliveryPhase.Delivered, value.OperationKey,
            value.OperationKey, NarrativeInferenceHash.ComputeSha256Utf8(value.OperationKey), string.Empty);
        failure = string.Empty;
        return true;
    }
    private sealed class Reservation : IDomainFactReservedIdentity
    {
        public Reservation(string key) => PublicationKey = key;
        public string PublicationKey { get; }
        public bool Used;
    }
    private sealed class Publication : IDomainFactPreparedPublication, IDomainFactPreparedIdentity
    {
        public Publication(GameplayPostCommitFactObserver<TFact> owner, string key, TFact fact)
        { CommitGroup = owner; OperationKey = key; Fact = fact; }
        public IDomainFactCommitGroup CommitGroup { get; }
        public string OperationKey { get; }
        public long OwnerRevision => 0;
        public bool IsReplay => false;
        public readonly TFact Fact;
        public bool Used;
        public void Cancel() => Used = true;
    }
}
