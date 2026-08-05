using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

public interface ICodexSaveQueryPort
{
    DungeonCodexSaveData Capture();
}

public interface ICodexRestorePort
{
    void Restore(DungeonCodexSaveData source);
}

public interface ICodexSaveSerializationPort
{
    string Serialize(DungeonCodexSaveData payload);
    DungeonCodexSaveData Deserialize(string payloadJson);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CodexSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "codex.entries";

    private readonly ICodexSaveQueryPort query;
    private readonly ICodexRestorePort restore;
    private readonly ICodexSaveSerializationPort serialization;

    public CodexSaveSection(
        ICodexSaveQueryPort query,
        ICodexRestorePort restore,
        ICodexSaveSerializationPort serialization)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.restore = restore ?? throw new ArgumentNullException(nameof(restore));
        this.serialization = serialization
            ?? throw new ArgumentNullException(nameof(serialization));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Array.Empty<string>();

    public string Capture()
    {
        DungeonCodexSaveData payload = query.Capture()
            ?? throw new InvalidOperationException(
                $"{SectionId} capture returned a null payload.");
        return serialization.Serialize(payload);
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
        RequireReport(report);
        RequireCurrentVersion(sectionVersion);
        ValidatePayload(serialization.Deserialize(payloadJson), report);
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireReport(report);
        RequireCurrentVersion(sectionVersion);
        DungeonCodexSaveData payload = serialization.Deserialize(payloadJson);
        ValidatePayload(payload, report);
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => restore.Restore(payload));
    }

    private static void ValidatePayload(
        DungeonCodexSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload == null || payload.entries == null)
        {
            report.AddError("Codex payload or entry list is null.");
            return;
        }

        HashSet<string> entryKeys = new HashSet<string>(StringComparer.Ordinal);
        CodexEntryCategory previousCategory = default;
        string previousEntryId = string.Empty;
        bool hasPrevious = false;
        foreach (DungeonCodexEntrySaveData entry in payload.entries)
        {
            string entryId = entry?.entryId ?? string.Empty;
            if (entry == null
                || entryId.Length == 0
                || !string.Equals(
                    entryId,
                    entryId.Trim(),
                    StringComparison.Ordinal)
                || entry.title == null
                || !string.Equals(
                    entry.title,
                    entry.title.Trim(),
                    StringComparison.Ordinal))
            {
                report.AddError(
                    "Codex payload contains a null or non-canonical entry.");
                continue;
            }

            if (!Enum.IsDefined(typeof(CodexEntryCategory), entry.category))
            {
                report.AddError(
                    $"Codex entry '{entryId}' has invalid category {(int)entry.category}.");
            }

            string key = $"{entry.category}:{entryId}";
            if (!entryKeys.Add(key))
            {
                report.AddError($"Codex payload contains duplicate entry '{key}'.");
            }
            if (hasPrevious
                && ((int)entry.category < (int)previousCategory
                    || (entry.category == previousCategory
                        && string.CompareOrdinal(previousEntryId, entryId) >= 0)))
            {
                report.AddError(
                    "Codex entries are not in canonical category/ID order.");
            }
            else
            {
                previousCategory = entry.category;
                previousEntryId = entryId;
                hasPrevious = true;
            }

            if (entry.lines == null)
            {
                report.AddError($"Codex entry '{key}' has no line list.");
                continue;
            }

            HashSet<string> lineKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (DungeonCodexLineSaveData line in entry.lines)
            {
                string text = line?.text ?? string.Empty;
                if (line == null
                    || string.IsNullOrWhiteSpace(text)
                    || !string.Equals(text, text.Trim(), StringComparison.Ordinal)
                    || !Enum.IsDefined(typeof(CodexInfoSource), line.source)
                    || !lineKeys.Add($"{(int)line.source}:{text}"))
                {
                    report.AddError(
                        $"Codex entry '{key}' has a null, duplicate, or invalid line.");
                }
            }
        }
    }

    private void RequireCurrentVersion(int sectionVersion)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {SectionId} section version {sectionVersion}.");
        }
    }

    private static void RequireReport(DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
    }
}
