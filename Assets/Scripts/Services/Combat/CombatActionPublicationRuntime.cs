using System;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

/// <summary>The combat authority keeps applied facts; the observer owns all log identities and policy.</summary>
public sealed class CombatActionPublicationRuntime : IStartable, ITickable, IDungeonSaveRestoreCompletedHook
{
    private readonly DungeonRuntimeAggregateRootStore roots;
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private DomainFactLease active;
    private CombatActionFact activeProposal;
    private float nextRecovery;
    private string lastFailure = string.Empty;
    private int recoveryCursor;
    public CombatActionPublicationRuntime(DungeonRuntimeAggregateRootStore roots,
        IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    {
        this.roots = roots ?? throw new ArgumentNullException(nameof(roots));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }
    private CombatActionPendingState Current => roots.GetOrCreate(() => new CombatActionPendingState());
    private CombatActionPendingState Writable => roots.GetOrCreateWritable(() => new CombatActionPendingState(),
        state => CombatActionPendingState.Restore(state.Capture()));
    public CombatActionPendingSaveData Capture()
    {
        if (activeProposal != null) throw new InvalidOperationException("combat-action-capture-during-mutation");
        return Current.Capture();
    }
    public CombatActionPendingState PrepareRestore(CombatActionPendingSaveData save)
    {
        if (activeProposal != null) throw new InvalidOperationException("combat-action-restore-during-mutation");
        return CombatActionPendingState.Restore(save);
    }
    public void PublishRestore(CombatActionPendingState state)
    {
        if (activeProposal != null) throw new InvalidOperationException("combat-action-restore-during-mutation");
        roots.Replace(state ?? throw new ArgumentNullException(nameof(state)));
    }
    public bool TryReserve(CombatActionFact proposal, out ReservedCombatAction reserved, out string failure)
    {
        reserved = default;
        if (proposal == null || !proposal.IsProposal || roots.IsRestoreStaging || activeProposal != null
            || Current.Count >= CombatActionPendingState.Capacity || Current.ContainsOperation(proposal.Identity))
        { failure = "combat-action-reservation-unavailable-or-already-applied"; return false; }
        if (!publisher.TryReserve(proposal, out var lease, out failure)) return false;
        if (string.IsNullOrWhiteSpace(lease.PublicationKey))
        { publisher.Cancel(lease); failure = "combat-action-publication-key-missing"; return false; }
        active = lease;
        activeProposal = proposal;
        reserved = new ReservedCombatAction(lease, proposal);
        return true;
    }
    public void Cancel(in ReservedCombatAction reserved)
    {
        try { publisher.Cancel(reserved.Lease); }
        catch (Exception exception) when (Recoverable(exception))
        { Debug.LogError("combat-action-reservation-release-failed:" + exception); }
        finally { if (ReferenceEquals(activeProposal, reserved.Proposal)) { active = default; activeProposal = null; } }
    }
    public DomainFactDelivery Publish(in ReservedCombatAction reserved, CombatActionFact fact)
    {
        if (!reserved.IsValid || !ReferenceEquals(activeProposal, reserved.Proposal)
            || fact == null || fact.IsProposal || !fact.MatchesProposal(reserved.Proposal))
        { Cancel(reserved); return DomainFactDelivery.Reject("combat-action-proposal-mismatch"); }
        string key = reserved.Lease.PublicationKey;
        try { Writable.Add(new CombatActionPendingData { publicationKey = key, fact = fact.Snapshot() }); }
        catch { Cancel(reserved); throw; }
        DomainFactDelivery delivery;
        try { delivery = publisher.Publish(reserved.Lease, fact); }
        catch (Exception exception) when (Recoverable(exception))
        { delivery = DomainFactDelivery.Uncertain("combat-action-observer-reply:" + exception.GetType().Name); }
        try { publisher.Cancel(reserved.Lease); }
        catch (Exception exception) when (Recoverable(exception))
        { Debug.LogError("combat-action-reservation-release-failed:" + exception); }
        if (!delivery.MayBeCommitted) Writable.Remove(key);
        else
        {
            try
            {
                if (IsDelivered(key, delivery)) Writable.Remove(key);
                else Report("combat-action-observation-pending:" + fact.Identity + ":" + delivery.Detail);
            }
            catch (Exception exception) when (Recoverable(exception))
            { return DomainFactDelivery.Uncertain("combat-action-acknowledgement-pending:" + exception.GetType().Name); }
        }
        return delivery;
    }
    public void Start() => Recover();
    public void OnRestoreCompleted() { recoveryCursor = 0; nextRecovery = 0; lastFailure = string.Empty; Recover(); }
    public void Tick()
    {
        if (roots.IsRestoreStaging || activeProposal != null || Current.Count == 0 || Time.unscaledTime < nextRecovery) return;
        nextRecovery = Time.unscaledTime + 1f;
        Recover();
    }
    public bool Recover()
    {
        if (roots.IsRestoreStaging || activeProposal != null) return false;
        var saved = Current.Capture().pending;
        if (saved.Count == 0) { recoveryCursor = 0; lastFailure = string.Empty; return true; }
        int start = recoveryCursor % saved.Count, count = Math.Min(8, saved.Count);
        string failure = string.Empty;
        for (int index = 0; index < count; index++)
        {
            var item = saved[(start + index) % saved.Count];
            var replies = new DomainFactDelivery[1];
            try
            {
                if (recovery.ReplayBatch(new[] { DomainFactReplay.Create(item.publicationKey,
                        new CombatActionFact(item.fact)) }, replies, out string reason)
                    && IsDelivered(item.publicationKey, replies[0])) Writable.Remove(item.publicationKey);
                else failure = "combat-action-recovery-pending:" + reason + ":" + replies[0].Detail;
            }
            catch (Exception exception) when (Recoverable(exception))
            { failure = "combat-action-recovery:" + exception.GetType().Name; }
        }
        recoveryCursor = (start + count) % saved.Count;
        if (failure.Length > 0) Report(failure); else lastFailure = string.Empty;
        return failure.Length == 0;
    }
    private void Report(string failure)
    { if (lastFailure != failure) Debug.LogWarning(failure); lastFailure = failure; }
    private static bool IsDelivered(string key, DomainFactDelivery result)
    {
        if (!result.Completed || result.OperationKey != key || string.IsNullOrWhiteSpace(result.PublicationId)
            || result.Fingerprint?.Length != 64) return false;
        foreach (char c in result.Fingerprint)
            if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')) return false;
        return true;
    }
    private static bool Recoverable(Exception value) => value is not OutOfMemoryException
        && value is not StackOverflowException && value is not AccessViolationException;
}
