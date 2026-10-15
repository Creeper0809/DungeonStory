/// <summary>
/// The immutable replay view of the authored action module catalog. Add a complete
/// action binding to GameplayActionLogCatalog, not a branch in the durable recorder.
/// Unsupported outcomes remain explicitly unassessed rather than inheriting an old score.
/// This registration and each observer's semantics are frozen for policy v3.
/// Extending/reinterpreting coverage requires a new policy version, not a silent
/// reassessment of an already serialized request.
/// </summary>
public static class GameplayExperienceObserverRegistration
{
    public static GameplayExperienceObserverRegistry Default { get; } =
        GameplayActionLogCatalog.CreateExperienceObservers();
}
