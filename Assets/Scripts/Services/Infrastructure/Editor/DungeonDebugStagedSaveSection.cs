using System;
using System.Collections.Generic;

internal abstract class DungeonDebugStagedSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection
{
    private const string MarkerJson = "{\"marker\":1}";

    public abstract string SectionId { get; }
    public virtual int SectionVersion => 1;
    public abstract DungeonSaveRestorePhase RestorePhase { get; }
    public virtual IReadOnlyList<string> DependsOn => Array.Empty<string>();

    public string Capture()
    {
        return MarkerJson;
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IDungeonSaveRestoreStage stage = StageRestore(
            payloadJson,
            sectionVersion,
            report);
        if (report.Success)
        {
            stage.Commit(report);
        }
    }

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {SectionId} debug section version {sectionVersion}.");
        }

        string canonical = payloadJson?.Trim();
        if (string.IsNullOrEmpty(canonical)
            || canonical[0] != '{'
            || canonical[canonical.Length - 1] != '}')
        {
            throw new InvalidOperationException(
                $"{SectionId} debug marker payload is not a JSON object.");
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            CommitMarker);
    }

    protected abstract void CommitMarker(DungeonGameRestoreReport report);
}
