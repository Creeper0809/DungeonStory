using System;
using System.Collections.Generic;

public sealed class ExperiencePacingAggregateState
{
    public HashSet<ExperienceEventConcept> IntroducedConcepts { get; } = new();
    public int CurrentDay { get; set; } = 1;
    public int ScheduledRehearsalMask { get; set; }
    public int CompletedRehearsalMask { get; set; }
    public int ActiveRehearsalDay { get; set; }
}

public sealed class DungeonRunFlowAggregateState
{
    public DungeonRunPhase Phase = DungeonRunPhase.Preparation;
    public DungeonRunOutcome Outcome;
    public int CurrentDay = 1;
    public int BossCycle;
    public bool BossArmed;
    public bool BossActive;
}

public sealed class DungeonDebugModeState
{
    public HashSet<DungeonDebugCheat> EnabledCheats { get; } = new();
    public HashSet<DungeonDebugOverlayKind> EnabledOverlays { get; } = new();
    public List<DungeonDebugCommandHistorySaveData> RecentCommands { get; } =
        new();
    public bool DebugModified { get; set; }
    public DungeonDebugOverlayScope OverlayScope { get; set; } =
        DungeonDebugOverlayScope.SelectedOnly;
}

public sealed class DungeonDebugRestoreCandidate
{
    public DungeonDebugRestoreCandidate(
        DungeonDebugModeState state,
        DungeonDebugRunSaveData payload = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Payload = payload;
    }

    public DungeonDebugModeState State { get; }
    public DungeonDebugRunSaveData Payload { get; }
}
