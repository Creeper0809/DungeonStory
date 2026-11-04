
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

// 런타임 상태와 Unity 오브젝트를 조회하지 않는 검사에만 명시한다.
// worker가 소유한 읽기 세션에서 private 캡처 DTO만 읽는다.
// 공유 report를 포함한 기존 검사 순서는 바꾸지 않는다.
public interface IDungeonDetachedCapturedSavePreflightValidator :
    IDungeonCapturedSavePreflightValidator
{
}

public interface IDungeonSaveRestoreCompletedHook
{
    void OnRestoreCompleted();
}
