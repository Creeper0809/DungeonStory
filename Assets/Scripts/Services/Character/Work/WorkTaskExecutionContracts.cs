using System;
using DungeonStory.Foundation;

public sealed class WorkTaskCoreDependencies
{
    public WorkTaskCoreDependencies(
        AbilityWork work,
        WorkTargetSelector targetSelector,
        IGameClock gameClock,
        IDungeonDebugRuleQuery debugRules)
    {
        Work = work ?? throw new ArgumentNullException(nameof(work));
        TargetSelector = targetSelector ?? throw new ArgumentNullException(nameof(targetSelector));
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        DebugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
    }

    public AbilityWork Work { get; }
    public WorkTargetSelector TargetSelector { get; }
    public IGameClock GameClock { get; }
    public IDungeonDebugRuleQuery DebugRules { get; }
}

public sealed class WorkTaskExecutionDependencies
{
    public WorkTaskExecutionDependencies(
        IWorkExecutionHandlerRegistry executionHandlers,
        IWorkOrderRuntime workOrderRuntime,
        IWorkAmountCalculator workAmountCalculator,
        IPaidFacilityContractRuntime paidFacilityContracts,
        IBuildingStructuralIntegrityRuntime structuralIntegrity = null)
    {
        ExecutionHandlers = executionHandlers
            ?? throw new ArgumentNullException(nameof(executionHandlers));
        WorkOrderRuntime = workOrderRuntime
            ?? throw new ArgumentNullException(nameof(workOrderRuntime));
        WorkAmountCalculator = workAmountCalculator
            ?? throw new ArgumentNullException(nameof(workAmountCalculator));
        PaidFacilityContracts = paidFacilityContracts
            ?? throw new ArgumentNullException(nameof(paidFacilityContracts));
        StructuralIntegrity = structuralIntegrity;
    }

    public IWorkExecutionHandlerRegistry ExecutionHandlers { get; }
    public IWorkOrderRuntime WorkOrderRuntime { get; }
    public IWorkAmountCalculator WorkAmountCalculator { get; }
    public IPaidFacilityContractRuntime PaidFacilityContracts { get; }
    public IBuildingStructuralIntegrityRuntime StructuralIntegrity { get; }
}

public sealed class WorkTaskEnvironmentDependencies
{
    public WorkTaskEnvironmentDependencies(
        IRoomEnvironmentExperienceService roomEnvironmentExperienceService,
        ICharacterEnvironmentWorkContext characterEnvironment,
        IEnvironmentalWorkwearCommand environmentalWorkwearCommands,
        IEnvironmentWorkPolicy environmentWorkPolicy)
    {
        RoomEnvironmentExperienceService = roomEnvironmentExperienceService
            ?? throw new ArgumentNullException(nameof(roomEnvironmentExperienceService));
        CharacterEnvironment = characterEnvironment
            ?? throw new ArgumentNullException(nameof(characterEnvironment));
        EnvironmentalWorkwearCommands = environmentalWorkwearCommands
            ?? throw new ArgumentNullException(nameof(environmentalWorkwearCommands));
        EnvironmentWorkPolicy = environmentWorkPolicy
            ?? throw new ArgumentNullException(nameof(environmentWorkPolicy));
    }

    public IRoomEnvironmentExperienceService RoomEnvironmentExperienceService { get; }
    public ICharacterEnvironmentWorkContext CharacterEnvironment { get; }
    public IEnvironmentalWorkwearCommand EnvironmentalWorkwearCommands { get; }
    public IEnvironmentWorkPolicy EnvironmentWorkPolicy { get; }
}

public readonly struct EmergencyWorkSuspensionReceipt
{
    public EmergencyWorkSuspensionReceipt(
        WorkTypeId workTypeId,
        string targetBuildingId,
        long alertEpochId,
        bool progressExternallyPersisted,
        float inlineCompletedWork = 0f,
        float inlineRequiredWork = 0f,
        long outcomeOwnerRevision = 1L)
    {
        WorkTypeId = workTypeId;
        TargetBuildingId = targetBuildingId?.Trim() ?? string.Empty;
        AlertEpochId = alertEpochId;
        ProgressExternallyPersisted = progressExternallyPersisted;
        InlineCompletedWork = inlineCompletedWork;
        InlineRequiredWork = inlineRequiredWork;
        OutcomeOwnerRevision = outcomeOwnerRevision;
    }

    public WorkTypeId WorkTypeId { get; }
    public string TargetBuildingId { get; }
    public long AlertEpochId { get; }
    public bool ProgressExternallyPersisted { get; }
    public float InlineCompletedWork { get; }
    public float InlineRequiredWork { get; }
    public long OutcomeOwnerRevision { get; }
    public bool HasInlineProgress => InlineRequiredWork > 0f
        && InlineCompletedWork >= 0f
        && InlineCompletedWork < InlineRequiredWork;
    public bool IsValid => WorkTypeId.IsValid
        && !string.IsNullOrWhiteSpace(TargetBuildingId)
        && AlertEpochId > 0L
        && OutcomeOwnerRevision > 0L
        && (ProgressExternallyPersisted || HasInlineProgress);
}

public enum WorkPreWuExitKind
{
    None = 0,
    RoutineNeedInterrupt = 1,
    EmergencySuspension = 2,
    EnvironmentInterrupt = 3,
    WorkOrderLoopConditionRejected = 4,
    WorkOrderApplyRejected = 5,
    TimedLoopConditionRejected = 6,
    PersistentApplyRejected = 7,
    ExecutionHandlerRejected = 8
}

