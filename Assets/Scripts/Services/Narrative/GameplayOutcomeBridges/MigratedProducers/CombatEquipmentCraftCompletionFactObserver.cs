using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>Owns narrative identity; physical revision stays a separate publication guard.</summary>
public sealed class CombatEquipmentCraftCompletionFactObserver :
    IDurableDomainFactObserver<CombatEquipmentCraftCompletedFact>,
    IDurableDomainFactStagingObserver<CombatEquipmentCraftCompletedFact>,
    IDurableDomainFactRecoveryObserver<CombatEquipmentCraftCompletedFact>, IDomainFactObserverBinding
{
    private readonly MigratedProducerOutcomeRuntime runtime;
    private readonly GameplayDomainFactCommitGroup native;
    private readonly CommitGroup commits;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(CombatEquipmentCraftCompletedFact) });
    public CombatEquipmentCraftCompletionFactObserver(MigratedProducerOutcomeRuntime runtime,
        GameplayDomainFactCommitGroup native)
    { this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime)); this.native = native; commits = new CommitGroup(native); }
    public IDisposable Connect(DomainFactDispatcher dispatcher) => dispatcher.Subscribe("log:combat.craft-completion", this);
    internal static GameplayResultKey Key(CombatEquipmentCraftCompletedFact fact) => MigratedProducerOutcomeCatalog.ResultKey(
        MigratedProducerOutcomeKind.CombatEquipmentCraftCompletionResult, fact.OwnerIdentity, 1L);

    public bool TryReserve(in CombatEquipmentCraftCompletedFact fact, out object reservation, out string failure)
    {
        reservation = null;
        var capture = CombatEquipmentCraftCompletionFactMapping.Capture(fact);
        if (!runtime.TryReserveObserved(capture, out var prepared, out failure)) return false;
        if (prepared.ResultKey != Key(fact))
        { runtime.Cancel(prepared); failure = "combat-craft-observation-already-applied"; return false; }
        reservation = new Reservation(fact, prepared); return true;
    }
    public bool TryPrepare(in CombatEquipmentCraftCompletedFact fact, object reservation,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        if (reservation is not Reservation state || state.Publication != null
            || fact.ContentFingerprint() != state.Fact.ContentFingerprint())
        { failure = "combat-craft-observation-reservation-conflict"; return false; }
        var capture = CombatEquipmentCraftCompletionFactMapping.Capture(fact);
        if (!runtime.TryPrepareObserved(state.Prepared, capture.ResolveDetails(Key(fact)), native, out var item, out failure)) return false;
        state.Publication = item; prepared = new Publication(commits, item, fact.OwnerRevision); return true;
    }
    public bool TryStage(in CombatEquipmentCraftCompletedFact fact, out object reservation,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        reservation = null; prepared = null;
        if (!Prepare(fact, Key(fact).ToString(), out var item, out failure)) return false;
        reservation = new Reservation(fact, default) { Publication = item };
        prepared = new Publication(commits, item, fact.OwnerRevision); return true;
    }
    public bool TryPrepareReplay(string publicationKey, in CombatEquipmentCraftCompletedFact fact,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        if (!Prepare(fact, publicationKey, out var item, out failure)) return false;
        prepared = new Publication(commits, item, fact.OwnerRevision); return true;
    }
    private bool Prepare(CombatEquipmentCraftCompletedFact fact, string key,
        out GameplayDomainFactPublication prepared, out string failure)
    {
        prepared = null;
        if (key != Key(fact).ToString()) { failure = "combat-craft-observation-identity-conflict"; return false; }
        return runtime.TryPrepareObservedRecovery(CombatEquipmentCraftCompletionFactMapping.Capture(fact),
            key, native, out prepared, out failure);
    }
    public DomainFactDelivery Replay(in CombatEquipmentCraftCompletedFact fact) =>
        DomainFactDelivery.Reject("combat-craft-saved-publication-required");
    public void Cancel(object reservation)
    {
        if (reservation is not Reservation state) return;
        state.Publication?.Cancel(); runtime.Cancel(state.Prepared);
    }
    private sealed class Reservation : IDomainFactReservedIdentity
    {
        public Reservation(CombatEquipmentCraftCompletedFact fact, PreparedMigratedProducerOutcome prepared)
        { Fact = fact; Prepared = prepared; }
        public readonly CombatEquipmentCraftCompletedFact Fact;
        public readonly PreparedMigratedProducerOutcome Prepared;
        public GameplayDomainFactPublication Publication;
        public string PublicationKey => Publication?.OperationKey ?? Prepared.ResultKey.ToString();
    }
    private sealed class Publication : IDomainFactPreparedPublication, IDomainFactPreparedIdentity
    {
        public Publication(CommitGroup group, GameplayDomainFactPublication inner, long physicalRevision)
        { CommitGroup = group; Inner = inner; OwnerRevision = physicalRevision; }
        public GameplayDomainFactPublication Inner { get; }
        public IDomainFactCommitGroup CommitGroup { get; }
        public string OperationKey => Inner.OperationKey;
        public long OwnerRevision { get; }
        public bool IsReplay => Inner.IsReplay;
        public void Cancel() => Inner.Cancel();
    }
    private sealed class CommitGroup : IDomainFactCommitGroup
    {
        private readonly GameplayDomainFactCommitGroup native;
        public CommitGroup(GameplayDomainFactCommitGroup native) => this.native = native ?? throw new ArgumentNullException(nameof(native));
        public bool Commit(IReadOnlyList<IDomainFactPreparedPublication> values, DomainFactDelivery[] results, out string failure)
        {
            if (values == null || values.Count == 0 || values.Any(v => v is not Publication || !ReferenceEquals(v.CommitGroup, this)))
                throw new ArgumentException("combat-craft-observation-commit-group-conflict");
            return native.Commit(values.Cast<Publication>().Select(v => (IDomainFactPreparedPublication)v.Inner).ToArray(), results, out failure);
        }
    }
}
