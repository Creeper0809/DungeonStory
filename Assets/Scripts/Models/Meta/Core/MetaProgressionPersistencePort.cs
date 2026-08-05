public interface IMetaProgressionPersistencePort
{
    MetaProgressionState State { get; }
    RunResultSnapshot LatestResult { get; }
    MetaRunProgressTracker RunProgress { get; }
    bool HasEnded { get; }
    MetaProgressionRestoreCandidate PrepareRestore(
        DungeonMetaProgressionSaveData data);
    void Restore(MetaProgressionRestoreCandidate candidate);
}
