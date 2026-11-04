public enum DungeonSaveOperationStatus
{
    Validating,
    Writing,
    Succeeded,
    Failed
}

// 접수와 완료를 구분한다. 상태 변경은 슬롯 서비스가 소유한다.
public interface IDungeonSaveOperation
{
    string SlotId { get; }
    string Path { get; }
    DungeonSaveOperationStatus Status { get; }
    bool IsCompleted { get; }
    bool Succeeded { get; }
    string Error { get; }
    DungeonDurableSaveCommitResult CommitResult { get; }
    double CaptureSeconds { get; }
    double LongestMainThreadStepSeconds { get; }
    int MainThreadStepCount { get; }
}
