public interface IMetaProgressionPersistencePort
{
    MetaProgressionState State { get; }
    RunResultSnapshot LatestResult { get; }
    MetaRunCompletionSnapshot PendingCompletion { get; }
    MetaRunProgressTracker RunProgress { get; }
    bool HasEnded { get; }
    MetaProgressionRestoreCandidate PrepareRestore(
        DungeonMetaProgressionSaveData data);
    void Restore(MetaProgressionRestoreCandidate candidate);
}
