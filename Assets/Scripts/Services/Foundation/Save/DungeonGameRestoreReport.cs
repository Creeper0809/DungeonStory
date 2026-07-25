using System;
using System.Collections.Generic;

public sealed class DungeonGameRestoreReport
{
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public IReadOnlyList<string> Warnings => warnings;
    public IReadOnlyList<string> Errors => errors;
    public bool Success => errors.Count == 0;
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
