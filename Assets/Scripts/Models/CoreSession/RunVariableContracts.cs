using System;
using System.Collections.Generic;
using System.Linq;

public enum DungeonDifficulty
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}

public enum DungeonSurvivalPressure
{
    Standard = 0,
    Relaxed = 1,
    Harsh = 2
}

public enum RunVariableCategory
{
    Start,
    Operation,
    Invasion
}

public enum EventAlertImportance
{
    Low,
    Medium,
    High
}

public interface IRunVariableEffect
{
}

public interface IRunVariableMultiplierEffect<in TContext> :
    IRunVariableEffect
{
    float GetMultiplier(TContext context);
}

public sealed class RunStartVariableSnapshot
{
    public RunStartVariableSnapshot(
        int seed,
        string ownerSpeciesTag,
        DungeonDifficulty difficulty,
        IReadOnlyList<int> startingFacilityCandidateIds,
        IReadOnlyList<string> startingGuestSpeciesCandidates,
        IReadOnlyList<int> startingBlueprintCandidateIds,
        int initialShopSeed,
        string initialDungeonLayoutId,
        float threatRiseMultiplier,
        string ownerDoctrineId = "",
        DungeonSurvivalPressure survivalPressure =
            DungeonSurvivalPressure.Standard)
    {
        this.seed = seed;
        this.ownerSpeciesTag = ownerSpeciesTag?.Trim() ?? string.Empty;
        runDifficulty = Enum.IsDefined(typeof(DungeonDifficulty), difficulty)
            ? difficulty
            : DungeonDifficulty.Normal;
        this.startingFacilityCandidateIds =
            EventPayloadSnapshot.Copy(startingFacilityCandidateIds);
        this.startingGuestSpeciesCandidates =
            EventPayloadSnapshot.Copy(startingGuestSpeciesCandidates);
        this.startingBlueprintCandidateIds =
            EventPayloadSnapshot.Copy(startingBlueprintCandidateIds);
        this.initialShopSeed = initialShopSeed;
        this.initialDungeonLayoutId =
            initialDungeonLayoutId?.Trim() ?? string.Empty;
        this.threatRiseMultiplier = Math.Max(0.05f, threatRiseMultiplier);
        this.ownerDoctrineId = ownerDoctrineId?.Trim() ?? string.Empty;
        this.survivalPressure =
            Enum.IsDefined(typeof(DungeonSurvivalPressure), survivalPressure)
                ? survivalPressure
                : DungeonSurvivalPressure.Standard;
    }

    public int seed { get; }
    public string ownerSpeciesTag { get; }
    public DungeonDifficulty runDifficulty { get; }
    public IReadOnlyList<int> startingFacilityCandidateIds { get; }
    public IReadOnlyList<string> startingGuestSpeciesCandidates { get; }
    public IReadOnlyList<int> startingBlueprintCandidateIds { get; }
    public int initialShopSeed { get; }
    public string initialDungeonLayoutId { get; }
    public float threatRiseMultiplier { get; }
    public string ownerDoctrineId { get; }
    public DungeonSurvivalPressure survivalPressure { get; }
}

public sealed class RunVariableDefinition
{
    public RunVariableDefinition(
        string id,
        RunVariableCategory category,
        string title,
        string detail,
        EventAlertImportance importance,
        int activeDays,
        IReadOnlyList<IRunVariableEffect> effects)
    {
        this.id = id?.Trim() ?? string.Empty;
        this.category = category;
        this.title = title?.Trim() ?? string.Empty;
        this.detail = detail?.Trim() ?? string.Empty;
        this.importance = importance;
        this.activeDays = Math.Max(1, activeDays);
        this.effects = EventPayloadSnapshot.Copy(effects);
    }

    public string id { get; }
    public RunVariableCategory category { get; }
    public string title { get; }
    public string detail { get; }
    public EventAlertImportance importance { get; }
    public int activeDays { get; }
    public IReadOnlyList<IRunVariableEffect> effects { get; }
}

public sealed class ActiveRunVariable
{
    public ActiveRunVariable(RunVariableDefinition definition, int startDay)
        : this(
            definition,
            startDay,
            Math.Max(1, definition?.activeDays ?? 1))
    {
    }

    public ActiveRunVariable(
        RunVariableDefinition definition,
        int startDay,
        int remainingDays)
    {
        Definition = definition;
        StartDay = Math.Max(1, startDay);
        RemainingDays = Math.Max(0, remainingDays);
    }

    public RunVariableDefinition Definition { get; }
    public int StartDay { get; }
    public int RemainingDays { get; private set; }
    public bool IsExpired => RemainingDays <= 0;

    public void AdvanceDay()
    {
        RemainingDays = Math.Max(0, RemainingDays - 1);
    }
}

public interface IRunVariableStateView
{
    RunStartVariableSnapshot StartVariables { get; }
    IReadOnlyList<ActiveRunVariable> ActiveOperationVariables { get; }
    RunVariableDefinition CurrentInvasionVariable { get; }
    bool HasStarted { get; }
}

public sealed class RunVariableState : IRunVariableStateView
{
    private readonly List<ActiveRunVariable> activeOperationVariables = new();
    private readonly IReadOnlyList<ActiveRunVariable>
        activeOperationVariablesView;

    public RunVariableState()
    {
        activeOperationVariablesView = activeOperationVariables.AsReadOnly();
    }

    public RunStartVariableSnapshot StartVariables { get; private set; }
    public IReadOnlyList<ActiveRunVariable> ActiveOperationVariables =>
        activeOperationVariablesView;
    public RunVariableDefinition CurrentInvasionVariable { get; private set; }
    public bool HasStarted => StartVariables != null;

    public void SetStartVariables(RunStartVariableSnapshot snapshot)
    {
        StartVariables = snapshot;
    }

    public ActiveRunVariable ActivateOperationVariable(
        RunVariableDefinition definition,
        int day)
    {
        if (definition == null
            || definition.category != RunVariableCategory.Operation)
        {
            return null;
        }

        activeOperationVariables.RemoveAll(active => active == null
            || active.Definition == null
            || string.Equals(
                active.Definition.id,
                definition.id,
                StringComparison.Ordinal));
        ActiveRunVariable instance = new(definition, day);
        activeOperationVariables.Add(instance);
        return instance;
    }

    public IReadOnlyList<ActiveRunVariable> AdvanceOperationVariables()
    {
        List<ActiveRunVariable> expired = new();
        foreach (ActiveRunVariable active in activeOperationVariables)
        {
            active.AdvanceDay();
            if (active.IsExpired)
            {
                expired.Add(active);
            }
        }

        activeOperationVariables.RemoveAll(active =>
            active == null || active.IsExpired);
        return expired;
    }

    public void SetInvasionVariable(RunVariableDefinition definition)
    {
        CurrentInvasionVariable = definition != null
            && definition.category == RunVariableCategory.Invasion
                ? definition
                : null;
    }

    public void ClearInvasionVariable()
    {
        CurrentInvasionVariable = null;
    }

    public void Restore(
        RunStartVariableSnapshot startVariables,
        IEnumerable<ActiveRunVariable> operationVariables,
        RunVariableDefinition invasionVariable)
    {
        StartVariables = startVariables;
        activeOperationVariables.Clear();
        activeOperationVariables.AddRange(
            (operationVariables ?? Array.Empty<ActiveRunVariable>())
            .Where(active => active != null
                && active.Definition != null
                && active.Definition.category == RunVariableCategory.Operation
                && !active.IsExpired));
        SetInvasionVariable(invasionVariable);
    }
}

public sealed class RunVariableAggregateState
{
    public RunVariableAggregateState(int runSeed, int currentDay = 1)
    {
        Variables = new RunVariableState();
        RunSeed = runSeed != 0 ? runSeed : 1;
        CurrentDay = Math.Max(1, currentDay);
    }

    public RunVariableState Variables { get; }
    public int RunSeed { get; set; }
    public int CurrentDay { get; set; }
}

[Serializable]
public sealed class DungeonRunVariableSaveData
{
    public const int CurrentVersion = 3;

    public int version = CurrentVersion;
    public int runSeed;
    public int currentDay = 1;
    public bool hasStartVariables;
    public DungeonRunStartSaveData startVariables;
    public List<DungeonActiveRunVariableSaveData> activeOperationVariables =
        new();
    public string invasionVariableId = string.Empty;
}

[Serializable]
public sealed class DungeonRunStartSaveData
{
    public int seed;
    public string ownerSpeciesTag = string.Empty;
    public string ownerDoctrineId = string.Empty;
    public DungeonDifficulty runDifficulty = DungeonDifficulty.Normal;
    public DungeonSurvivalPressure survivalPressure =
        DungeonSurvivalPressure.Standard;
    public List<int> startingFacilityCandidateIds = new();
    public List<string> startingGuestSpeciesCandidates = new();
    public List<int> startingBlueprintCandidateIds = new();
    public int initialShopSeed;
    public string initialDungeonLayoutId = string.Empty;
    public float threatRiseMultiplier = 1f;
}

[Serializable]
public sealed class DungeonActiveRunVariableSaveData
{
    public string definitionId = string.Empty;
    public int startDay = 1;
    public int remainingDays = 1;
}

[Serializable]
public sealed class DungeonRunFlowSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public DungeonRunPhase phase = DungeonRunPhase.Preparation;
    public DungeonRunOutcome outcome = DungeonRunOutcome.None;
    public int currentDay = 1;
    public bool bossArmed;
    public bool bossActive;
    public int bossCycle;
}

public interface IRunVariableRuntime
{
    int RunSeed { get; }
    int CurrentDay { get; }
    DungeonRunVariableSaveData CaptureForSave();
    void RestoreFromSave(DungeonRunVariableSaveData saveData);
}
