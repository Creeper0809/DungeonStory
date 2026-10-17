using System;
using System.Collections.Generic;

public enum DungeonWorldRestoreOutcome
{
    NotApplied,
    Applied,
    AppliedRecoveryRequired,
    RollbackRecoveryRequired
}

public interface IDungeonRestoreSafetyQuery
{
    bool RequiresRecovery { get; }
    string RecoveryMessage { get; }
}

public sealed class DungeonGameRestoreReport
{
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();
    private bool rollbackRecoveryRequired;

    public IReadOnlyList<string> Warnings => warnings;
    public IReadOnlyList<string> Errors => errors;
    public bool Success => errors.Count == 0;
    public bool WorldPublished { get; private set; }
    public bool RequiresRecovery => rollbackRecoveryRequired || (WorldPublished && !Success);
    public DungeonWorldRestoreOutcome Outcome => rollbackRecoveryRequired
        ? DungeonWorldRestoreOutcome.RollbackRecoveryRequired
        : !WorldPublished
        ? DungeonWorldRestoreOutcome.NotApplied
        : RequiresRecovery
            ? DungeonWorldRestoreOutcome.AppliedRecoveryRequired
            : DungeonWorldRestoreOutcome.Applied;
    public string FailureDescription => rollbackRecoveryRequired
        ? "복원에 실패했고 이전 월드도 완전히 되돌리지 못했습니다. 저장하지 않고 타이틀로 돌아가 다시 불러와 주세요."
        : RequiresRecovery
            ? "새 월드는 적용됐지만 복원 후처리를 마치지 못했습니다. 저장하지 않고 타이틀로 돌아가 다시 불러와 주세요."
            : string.Join("\n", errors);
    public int RestoredBuildingCount { get; private set; }
    public int RestoredCharacterCount { get; private set; }
    public int RestoredExpeditionCount { get; private set; }
    public int RestoredIntruderCount { get; private set; }

    public void AddWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            warnings.Add(message);
        }
    }

    internal void RecordWorldPublished() => WorldPublished = true;
    internal void RecordRollbackFailure() => rollbackRecoveryRequired = true;

    public void AddError(string message)
    {
        errors.Add(string.IsNullOrWhiteSpace(message)
            ? "Unknown restore error."
            : message);
    }

    public void RecordRestoredBuildings(int count)
    {
        RestoredBuildingCount = Math.Max(0, count);
    }

    public void RecordRestoredCharacters(int count)
    {
        RestoredCharacterCount = Math.Max(0, count);
    }

    public void RecordRestoredExpeditions(int count)
    {
        RestoredExpeditionCount = Math.Max(0, count);
    }

    public void RecordRestoredIntruders(int count)
    {
        RestoredIntruderCount = Math.Max(0, count);
    }
}
