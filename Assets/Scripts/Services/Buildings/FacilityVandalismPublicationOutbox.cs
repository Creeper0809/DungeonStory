using System;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

public sealed class FacilityVandalismPublicationOutbox : IStartable, ITickable, IDungeonSaveRestoreCompletedHook
{
    private readonly DungeonRuntimeAggregateRootStore roots;
    private readonly IDomainFactRecoveryPublisher recovery;
    private float nextRecovery;
    private int cursor;
    private string lastFailure = string.Empty;
    public FacilityVandalismPublicationOutbox(DungeonRuntimeAggregateRootStore roots, IDomainFactRecoveryPublisher recovery)
    {
        this.roots = roots ?? throw new ArgumentNullException(nameof(roots));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }
    private FacilityVandalismPendingState Current => roots.GetOrCreate(() => new FacilityVandalismPendingState());
    private FacilityVandalismPendingState Writable => roots.GetOrCreateWritable(
        () => new FacilityVandalismPendingState(), value => FacilityVandalismPendingState.Restore(value.Capture()));
    public bool CanAccept => !roots.IsRestoreStaging && Current.Count < FacilityVandalismPendingState.Capacity;
    public FacilityVandalismPendingSaveData Capture() => Current.Capture();
    public FacilityVandalismPendingState PrepareRestore(FacilityVandalismPendingSaveData save) =>
        FacilityVandalismPendingState.Restore(save);
    public void PublishRestore(FacilityVandalismPendingState state) => roots.Replace(state ?? throw new ArgumentNullException(nameof(state)));
    public void Add(FacilityVandalismCommitState value) => Writable.Add(value);
    public void Remove(string key) => Writable.Remove(key);
    public bool Acknowledge(string key, DomainFactDelivery result)
    {
        if (!result.Completed || result.OperationKey != key
            || string.IsNullOrWhiteSpace(result.PublicationId) || string.IsNullOrWhiteSpace(result.Fingerprint)) return false;
        if (Current.Contains(key)) Writable.Remove(key);
        return true;
    }
    public void Start() => Recover();
    public void OnRestoreCompleted() { cursor = 0; nextRecovery = 0; lastFailure = string.Empty; Recover(); }
    public void Tick()
    {
        if (roots.IsRestoreStaging || Current.Count == 0 || Time.unscaledTime < nextRecovery) return;
        nextRecovery = Time.unscaledTime + 0.5f;
        Recover();
    }
    public bool Recover()
    {
        if (roots.IsRestoreStaging) return false;
        var pending = Capture().pending;
        if (pending.Count == 0) { cursor = 0; lastFailure = string.Empty; return true; }
        int start = cursor % pending.Count, count = Math.Min(8, pending.Count);
        string failure = string.Empty;
        for (int index = 0; index < count; index++)
        {
            var entry = pending[(start + index) % pending.Count];
            var results = new DomainFactDelivery[1];
            try
            {
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(entry.publicationKey,
                    new FacilityVandalismFact(entry)) }, results, out string detail)
                    || !Acknowledge(entry.publicationKey, results[0]))
                    failure = "facility-vandalism-delivery-pending:" + detail + ":" + results[0].Detail;
            }
            catch (Exception exception) { failure = "facility-vandalism-replay-failed:" + exception.Message; }
        }
        cursor = (start + count) % pending.Count;
        if (failure.Length > 0 && failure != lastFailure) Debug.LogWarning(failure);
        lastFailure = failure;
        return failure.Length == 0;
    }
}
