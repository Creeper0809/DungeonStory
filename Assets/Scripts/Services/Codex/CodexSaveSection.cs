using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CodexSaveSection :
    DungeonJsonSaveSection<DungeonCodexSaveData>
{
    public const string Id = "codex.entries";

    private readonly ICodexRuntimeProvider runtimeProvider;

    public CodexSaveSection(ICodexRuntimeProvider runtimeProvider)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
    }

    public override string SectionId => Id;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;

    protected override DungeonCodexSaveData CapturePayload()
    {
        DungeonCodexSaveData destination = new DungeonCodexSaveData();
        if (!runtimeProvider.TryGetRuntime(out CodexRuntime runtime))
        {
            return destination;
        }

        destination.entries = runtime.State.Entries
            .OrderBy(entry => entry.Category)
            .ThenBy(entry => entry.EntryId, StringComparer.Ordinal)
            .Select(entry => new DungeonCodexEntrySaveData
            {
                category = entry.Category,
                entryId = entry.EntryId,
                title = entry.Title,
                lines = entry.Lines.Select(line => new DungeonCodexLineSaveData
                {
                    text = line.Text,
                    source = line.Source
                }).ToList()
            })
            .ToList();
        return destination;
    }

    protected override void RestorePayload(
        DungeonCodexSaveData source,
        DungeonGameRestoreReport report)
    {
        if (!runtimeProvider.TryGetRuntime(out CodexRuntime runtime))
        {
            report.AddWarning("Codex runtime was not present; codex state was skipped.");
            return;
        }

        runtime.State.ClearForRestore();
        foreach (DungeonCodexEntrySaveData entry in source.entries
                     ?? new List<DungeonCodexEntrySaveData>())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.entryId))
            {
                continue;
            }

            runtime.State.GetOrCreate(entry.category, entry.entryId, entry.title);
            foreach (DungeonCodexLineSaveData line in entry.lines
                         ?? new List<DungeonCodexLineSaveData>())
            {
                if (line != null)
                {
                    runtime.State.AddInfo(
                        entry.category,
                        entry.entryId,
                        entry.title,
                        line.text,
                        line.source);
                }
            }
        }
    }
}
