using System;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Owns the work module graph, its injected construction dependencies and invalidation.
/// AbilityWork retains Unity routines and all gameplay state held by existing modules.
/// </summary>
internal sealed class CharacterWorkModuleRuntime
{
    private readonly AbilityWork work;
    private const string WorkAccidentRandomStreamId = "character:work-accident";
    private WorkTargetSelector targetSelector;
    private WorkTaskExecutor taskExecutor;
    private WorkDutyController dutyController;
    private WorkCommandHandler commandHandler;
    private IRandomStream workAccidentRandom;
    private IWorkExecutionHandlerRegistry workExecutionHandlerRegistry;
    private IWorkPolicyRegistry workPolicyRegistry;
    private ICharacterNeedDefinitionCatalog needDefinitionCatalog;
    private IWorkOrderRuntime workOrderRuntime;
    private IPaidFacilityContractRuntime paidFacilityContracts;
    private IEnvironmentWorkPolicy environmentWorkPolicy;
    private IDungeonDebugRuleQuery debugRules;
    private ICharacterEnvironmentWorkContext characterEnvironment;
    private IEnvironmentalWorkwearCommand environmentalWorkwearCommands;
    private IWorkAmountCalculator workAmountCalculator;
    private ICaptiveLaborQuery captiveLaborQuery;
    private IGameClock gameClock;
    private IGameCalendar gameCalendar;
    private ICharacterProficiencyCommand proficiencyCommands;
    private ICharacterProficiencyQuery proficiencyQuery;
    private ICombatEquipmentRuntime combatEquipmentRuntime;
    private IDefenseEngagementRuntime defenseEngagementRuntime;
    private IRoomEnvironmentExperienceService roomEnvironmentExperienceService;
    private CharacterIdentityEventPublisher identityEvents;
    private ICharacterPerformanceQuery performance;
    private CharacterWorkPerformanceContextResolver performanceContext;
    private IAnatomyHealthRuntime anatomyHealth;
    private ICharacterSpeciesCommand speciesCommands;
    private IEmergencyWorkAccountingService emergencyWorkAccounting;
    private ISettlementLaborAccountingService settlementLaborAccounting;
    private IReservedItemTransferService reservedItemTransfers;
    private ICharacterSettlementStandingQuery settlementStandings;
    private IBuildingStructuralIntegrityRuntime structuralIntegrity;
    private ICharacterBodyHealthMutationTransaction bodyHealthMutation;
    private IDomainFactPublisher accidentFacts;
    private WorkAccidentObservationRuntime workAccidentObservations;

    internal CharacterWorkModuleRuntime(AbilityWork work)
    {
        this.work = work;
    }

    internal WorkTargetSelector TargetSelector => targetSelector;
    internal WorkTaskExecutor TaskExecutor => taskExecutor;
    internal WorkDutyController DutyController => dutyController;
    internal WorkCommandHandler CommandHandler => commandHandler;
    internal IWorkExecutionHandlerRegistry WorkExecutionHandlerRegistry => workExecutionHandlerRegistry;
    internal IWorkPolicyRegistry WorkPolicyRegistry => workPolicyRegistry;
    internal IWorkOrderRuntime WorkOrderRuntime => workOrderRuntime;
    internal IGameClock GameClock => gameClock;
    internal IGameCalendar GameCalendar => gameCalendar;
    internal ICharacterProficiencyCommand ProficiencyCommands => proficiencyCommands;
    internal ICombatEquipmentRuntime CombatEquipmentRuntime => combatEquipmentRuntime;
    internal CharacterIdentityEventPublisher IdentityEvents => identityEvents;

    internal void ConstructAbilityWork(
        IWorkExecutionHandlerRegistry workExecutionHandlerRegistry,
        IWorkPolicyRegistry workPolicyRegistry,
        IWorkOrderRuntime workOrderRuntime,
        IWorkAmountCalculator workAmountCalculator,
        ICaptiveLaborQuery captiveLaborQuery,
        IGameClock gameClock,
        IDefenseEngagementRuntime defenseEngagementRuntime,
        IRoomEnvironmentExperienceService roomEnvironmentExperienceService,
        IPaidFacilityContractRuntime paidFacilityContracts,
        IEnvironmentWorkPolicy environmentWorkPolicy,
        ICharacterEnvironmentWorkContext characterEnvironment,
        IEnvironmentalWorkwearCommand environmentalWorkwearCommands,
        ICharacterNeedDefinitionCatalog needDefinitionCatalog,
        IDungeonDebugRuleQuery debugRules)
    {
        this.workExecutionHandlerRegistry = workExecutionHandlerRegistry;
        this.workPolicyRegistry = workPolicyRegistry;
        this.workOrderRuntime = workOrderRuntime;
        this.workAmountCalculator = workAmountCalculator;
        this.captiveLaborQuery = captiveLaborQuery;
        this.gameClock = gameClock;
        this.defenseEngagementRuntime = defenseEngagementRuntime;
        this.roomEnvironmentExperienceService = roomEnvironmentExperienceService;
        this.paidFacilityContracts = paidFacilityContracts;
        this.environmentWorkPolicy = environmentWorkPolicy;
        this.characterEnvironment = characterEnvironment
            ?? throw new ArgumentNullException(nameof(characterEnvironment));
        this.environmentalWorkwearCommands = environmentalWorkwearCommands
            ?? throw new ArgumentNullException(nameof(environmentalWorkwearCommands));
        this.needDefinitionCatalog = needDefinitionCatalog
            ?? throw new ArgumentNullException(nameof(needDefinitionCatalog));
        this.debugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
        targetSelector = null;
        taskExecutor = null;
        dutyController = null;
        commandHandler = null;
    }

    internal void ConstructIdentityEvents(
        CharacterIdentityEventPublisher identityEvents)
    {
        this.identityEvents = identityEvents
            ?? throw new ArgumentNullException(nameof(identityEvents));
        taskExecutor = null;
    }

    internal void ConstructPerformance(
        ICharacterPerformanceQuery performance,
        CharacterWorkPerformanceContextResolver performanceContext,
        IAnatomyHealthRuntime anatomyHealth,
        ICharacterSpeciesCommand speciesCommands)
    {
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.performanceContext = performanceContext
            ?? throw new ArgumentNullException(nameof(performanceContext));
        this.anatomyHealth = anatomyHealth
            ?? throw new ArgumentNullException(nameof(anatomyHealth));
        this.speciesCommands = speciesCommands
            ?? throw new ArgumentNullException(nameof(speciesCommands));
        taskExecutor = null;
    }

    internal void ConstructEmergencyWorkAccounting(
        IEmergencyWorkAccountingService emergencyWorkAccounting,
        ISettlementLaborAccountingService settlementLaborAccounting)
    {
        this.emergencyWorkAccounting = emergencyWorkAccounting
            ?? throw new ArgumentNullException(nameof(emergencyWorkAccounting));
        this.settlementLaborAccounting = settlementLaborAccounting
            ?? throw new ArgumentNullException(nameof(settlementLaborAccounting));
        taskExecutor = null;
    }

    internal void ConstructReservedItemTransfers(
        IReservedItemTransferService reservedItemTransfers)
    {
        this.reservedItemTransfers = reservedItemTransfers
            ?? throw new ArgumentNullException(nameof(reservedItemTransfers));
        taskExecutor = null;
    }

    internal void ConstructProficiencyProgression(
        ICharacterProficiencyCommand proficiencyCommands,
        ICharacterProficiencyQuery proficiencyQuery,
        IGameCalendar gameCalendar,
        ICombatEquipmentRuntime combatEquipmentRuntime)
    {
        this.proficiencyCommands = proficiencyCommands
            ?? throw new ArgumentNullException(nameof(proficiencyCommands));
        this.proficiencyQuery = proficiencyQuery
            ?? throw new ArgumentNullException(nameof(proficiencyQuery));
        this.gameCalendar = gameCalendar
            ?? throw new ArgumentNullException(nameof(gameCalendar));
        this.combatEquipmentRuntime = combatEquipmentRuntime;
        taskExecutor = null;
    }

    internal void ConstructSettlementStandings(
        ICharacterSettlementStandingQuery settlementStandings)
    {
        this.settlementStandings = settlementStandings
            ?? throw new ArgumentNullException(nameof(settlementStandings));
        targetSelector = null;
        taskExecutor = null;
    }

    internal void ConstructWorkAccidentRandom(
        IRandomStreamProvider randomStreamProvider)
    {
        workAccidentRandom = (randomStreamProvider
                ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get(WorkAccidentRandomStreamId);
        taskExecutor = null;
    }

    internal void ConstructWorkAccidentObservation(
        ICharacterBodyHealthMutationTransaction bodyHealthMutation,
        IDomainFactPublisher accidentFacts,
        WorkAccidentObservationRuntime workAccidentObservations)
    {
        this.bodyHealthMutation = bodyHealthMutation
            ?? throw new ArgumentNullException(nameof(bodyHealthMutation));
        this.accidentFacts = accidentFacts ?? throw new ArgumentNullException(nameof(accidentFacts));
        this.workAccidentObservations = workAccidentObservations
            ?? throw new ArgumentNullException(nameof(workAccidentObservations));
        taskExecutor = null;
    }

    internal void ConstructBuildingStructuralIntegrity(
        IBuildingStructuralIntegrityRuntime integrityRuntime)
    {
        structuralIntegrity = integrityRuntime
            ?? throw new ArgumentNullException(nameof(integrityRuntime));
        taskExecutor = null;
    }

    internal void EnsureReady()
    {
        if (needDefinitionCatalog == null)
        {
            return;
        }

        if (targetSelector != null
            && taskExecutor != null
            && dutyController != null
            && commandHandler != null)
        {
            return;
        }

        bool selectorChanged = targetSelector == null;
        targetSelector ??= new WorkTargetSelector(
            work,
            workPolicyRegistry,
            captiveLaborQuery,
            environmentWorkPolicy,
            settlementStandings);
        if (selectorChanged)
        {
            // Preserve existing command/run/cargo state while replacing selection policy.
            taskExecutor?.RebindTargetSelector(targetSelector);
            commandHandler?.RebindTargetSelector(targetSelector);
        }
        taskExecutor ??= new WorkTaskExecutor(
            new WorkTaskCoreDependencies(
                work,
                targetSelector,
                gameClock,
                debugRules),
            new WorkTaskExecutionDependencies(
                workExecutionHandlerRegistry,
                workOrderRuntime,
                workAmountCalculator,
                paidFacilityContracts,
                structuralIntegrity),
            new WorkTaskEnvironmentDependencies(
                roomEnvironmentExperienceService,
                characterEnvironment,
                environmentalWorkwearCommands,
                environmentWorkPolicy),
            proficiencyCommands,
            gameCalendar,
            proficiencyQuery,
            combatEquipmentRuntime,
            workAccidentRandom,
            identityEvents,
            performance,
            performanceContext,
            anatomyHealth,
            speciesCommands,
            emergencyWorkAccounting,
            settlementLaborAccounting,
            reservedItemTransfers,
            settlementStandings,
            bodyHealthMutation,
            accidentFacts,
            workAccidentObservations);
        dutyController ??= new WorkDutyController(
            work,
            needDefinitionCatalog);
        commandHandler ??= new WorkCommandHandler(work, targetSelector, defenseEngagementRuntime);
    }

    internal void InvalidateTargetSelection() => targetSelector = null;
}
