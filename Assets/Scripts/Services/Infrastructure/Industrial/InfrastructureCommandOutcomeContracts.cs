using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class DungeonInfrastructureObservationSaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public long nextOwnerRevision = 1;
    public List<InfrastructureObservationSaveData> pending = new();
}

// Owns only frozen domain facts and opaque acknowledgements, not native ledger data.
public interface IInfrastructureFactJournal
{
    IndustrialTransitionFactData CaptureDraft(IndustrialTransitionKind kind, string targetId, string facilityId,
        string facilityDisplayName, int x, int y, IndustrialStateData before, IndustrialStateData after,
        IndustrialTransitionDetails details = null);
    bool TryPrepare(IndustrialTransitionFactData draft, out IDomainFactStage prepared, out string failure);
    DomainFactDelivery CommitReversible(IDomainFactStage prepared);
    void QueueCommitted(IDomainFactStage prepared);
    void DeliverQueued(long ownerRevision, IDomainFactStage prepared = null);
    bool IsPending(long ownerRevision);
    void Cancel(IDomainFactStage prepared);
    void AssertIdle();
}

public interface IInfrastructureObservationPersistence
{
    DungeonInfrastructureObservationSaveData Capture();
    InfrastructureObservationRestoreCandidate PrepareRestore(DungeonInfrastructureObservationSaveData snapshot);
    void Restore(InfrastructureObservationRestoreCandidate candidate);
}

public sealed class InfrastructureObservationRestoreCandidate
{
    internal InfrastructureObservationRestoreCandidate(long nextOwnerRevision, IReadOnlyList<InfrastructureObservationSaveData> pending)
    { NextOwnerRevision = nextOwnerRevision; Pending = pending ?? throw new ArgumentNullException(nameof(pending)); }
    internal long NextOwnerRevision { get; }
    internal IReadOnlyList<InfrastructureObservationSaveData> Pending { get; }
}
