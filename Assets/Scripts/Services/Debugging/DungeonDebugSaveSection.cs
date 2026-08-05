using System;
using System.Collections.Generic;
using DungeonStory.Content.CoreSession;
public sealed class DungeonDebugSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonDebugRunSaveData,
        DungeonDebugRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "debug.run";

    private readonly IDungeonDebugModeService debugModeService;
    private readonly CoreSessionRulesDefinition rules;

    public DungeonDebugSaveSection(
        IDungeonDebugModeService debugModeService,
        ICoreSessionRulesProvider rulesProvider)
    {
        this.debugModeService = debugModeService
            ?? throw new ArgumentNullException(nameof(debugModeService));
        rules = (rulesProvider
                ?? throw new ArgumentNullException(nameof(rulesProvider)))
            .CoreSessionRules
            ?? throw new InvalidOperationException(
                "Core-session rules are not authored.");
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonDebugRunSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Presentation;
    public override IReadOnlyList<string> DependsOn =>
        new[] { RunFlowSaveSection.Id };

    protected override DungeonDebugRunSaveData CapturePayload() =>
        debugModeService.Capture();

    protected override DungeonDebugRestoreCandidate BuildRestoreCandidate(
        DungeonDebugRunSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        if (payload == null || payload.recentCommands == null)
        {
            report.AddError("Dungeon-debug payload or history list is null.");
        }
        else
        {
            if (payload.version != DungeonDebugRunSaveData.CurrentVersion)
            {
                report.AddError(
                    $"Dungeon-debug payload version {payload.version} is unsupported.");
            }
            if (payload.recentCommands.Count > rules.DebugHistoryLimit)
            {
                report.AddError(
                    "Dungeon-debug history exceeds the authored limit of "
                    + $"{rules.DebugHistoryLimit} entries.");
            }
            for (int index = 0; index < payload.recentCommands.Count; index++)
            {
                DungeonDebugCommandHistorySaveData entry =
                    payload.recentCommands[index];
                if (entry == null
                    || entry.gameTime == null
                    || entry.commandId == null
                    || entry.target == null
                    || entry.result == null)
                {
                    report.AddError(
                        $"Dungeon-debug history entry {index} is null or incomplete.");
                }
            }
        }

        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Dungeon-debug restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        return debugModeService.PrepareRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        DungeonDebugRestoreCandidate candidate) =>
        debugModeService.PublishRestoreCandidate(candidate);
}
