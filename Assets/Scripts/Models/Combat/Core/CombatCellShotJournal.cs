using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

/// <summary>
/// The command aggregate owns these applied, undelivered facts. A lost publication
/// response retains the mechanical result; recovery publishes facts, never effects.
/// </summary>
public static class CombatCellShotJournal
{
    public const int MaximumPending = 256;
    public static bool TryApply(List<CombatCellShotPendingData> pending, IDomainFactPublisher publisher,
        CombatCellShotFact fact, Func<string> apply, Func<bool> matchesApplied, Action rollback,
        out string failure)
    {
        if (pending == null || publisher == null || fact == null || apply == null || matchesApplied == null || rollback == null)
            throw new ArgumentNullException("Cell-shot transaction dependencies are required.");
        if (pending.Count >= MaximumPending)
        { failure = "combat-cell-shot-pending-capacity"; return false; }
        foreach (var entry in pending)
            if (new CombatCellShotFact(entry.fact).OperationId == fact.OperationId)
            { failure = "combat-cell-shot-already-applied-pending"; return false; }
        if (!publisher.TryReserve(fact, out var lease, out failure)) return false;
        if (string.IsNullOrWhiteSpace(lease.PublicationKey))
        { publisher.Cancel(lease); failure = "combat-cell-shot-publication-key-missing"; return false; }
        try
        {
            failure = apply();
            if (string.IsNullOrEmpty(failure) && !matchesApplied()) failure = "combat-cell-shot-post-state-mismatch";
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            failure = "combat-cell-shot-mutation:" + exception.GetType().Name;
        }
        if (!string.IsNullOrEmpty(failure))
        {
            publisher.Cancel(lease);
            rollback();
            return false;
        }
        var saved = new CombatCellShotPendingData { publicationKey = lease.PublicationKey, fact = fact.Snapshot() };
        pending.Add(saved); // Freeze before publication can commit or throw.
        DomainFactDelivery delivery;
        try { delivery = publisher.Publish(lease, fact); }
        catch (Exception exception) when (IsRecoverable(exception))
        { delivery = DomainFactDelivery.Uncertain("combat-cell-shot-publication:" + exception.GetType().Name); }
        // A wrapper may lose the response before reaching the dispatcher.
        // Release only the transient lease; Cancel cannot undo durable delivery.
        publisher.Cancel(lease);
        if (!delivery.MayBeCommitted)
        {
            pending.Remove(saved);
            rollback();
            failure = delivery.Detail;
            return false;
        }
        if (IsDelivered(saved, delivery)) pending.Remove(saved);
        failure = pending.Contains(saved) ? "combat-cell-shot-publication-pending:" + delivery.Detail : string.Empty;
        return true;
    }

    public static bool Resume(List<CombatCellShotPendingData> pending, IDomainFactRecoveryPublisher publisher,
        out string failure)
    {
        if (pending == null || publisher == null) throw new ArgumentNullException(nameof(pending));
        failure = string.Empty;
        for (int index = pending.Count - 1; index >= 0; index--)
        {
            var entry = pending[index];
            var results = new DomainFactDelivery[1];
            try
            {
                if (!publisher.ReplayBatch(new[] { DomainFactReplay.Create(entry.publicationKey, new CombatCellShotFact(entry.fact)) },
                        results, out failure) || !IsDelivered(entry, results[0]))
                {
                    if (string.IsNullOrEmpty(failure)) failure = "combat-cell-shot-recovery-pending:" + results[0].Detail;
                    return false; // Applied state never rolls back during recovery.
                }
            }
            catch (Exception exception) when (IsRecoverable(exception))
            { failure = "combat-cell-shot-recovery:" + exception.GetType().Name; return false; }
            pending.RemoveAt(index);
        }
        return true;
    }
    private static bool IsDelivered(CombatCellShotPendingData entry, DomainFactDelivery delivery) =>
        delivery.Completed && delivery.OperationKey == entry.publicationKey
        && !string.IsNullOrWhiteSpace(delivery.PublicationId) && IsFingerprint(delivery.Fingerprint);
    private static bool IsFingerprint(string value)
    {
        if (value == null || value.Length != 64) return false;
        foreach (char c in value) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')) return false;
        return true;
    }
    private static bool IsRecoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
}
