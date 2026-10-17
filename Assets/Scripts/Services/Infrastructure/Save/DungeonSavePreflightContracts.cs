
public interface IDungeonSavePreflightValidator
{
    void Validate(DungeonGameSaveData saveData, DungeonGameRestoreReport report);
}

public interface IDungeonSaveCaptureGuard
{
    void ValidateBeforeCapture();
}

/// <summary>
/// Validates the canonical, fully captured envelope graph before a save can be
/// returned to a slot writer. This is intentionally later than
/// <see cref="IDungeonSaveCaptureGuard"/> so cross-section validators inspect
/// the exact bytes and versions that would become durable.
/// </summary>
public interface IDungeonCapturedSavePreflightValidator
{
    void Validate(
        DungeonGameSaveData captured,
        DungeonGameRestoreReport report);
}

public interface IDungeonSaveRestoreCompletedHook
{
    void OnRestoreCompleted();
}
