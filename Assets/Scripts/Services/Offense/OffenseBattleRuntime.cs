using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using VContainer;
using VContainer.Unity;

public interface IOffenseBattleRuntime
{
    OffenseBattleSession Session { get; }
    bool HasActiveBattle { get; }
    bool IsBattleViewVisible { get; }
    event Action StateChanged;
    event Action<OffenseBattleSession> BattleCompleted;
    bool TryStartBattle(OffenseExpeditionRun expedition, out string message);
    void AdvanceToPlayerDecision();
    bool TryIssuePlayerCommand(
        OffenseBattleActionType actionType,
        string targetId,
        string abilityId,
        out OffenseBattleCommandResult result);
    bool TryExecuteCommand(OffenseBattleCommand command, out OffenseBattleCommandResult result);
    bool TryExecutePlannedCommand(
        int directorTurn,
        string actorId,
        string targetId,
        OffenseBattleActionType actionType,
        string abilityId,
        out OffenseBattleCommandResult result);
    bool FinalizePlannedTurn(int directorTurn, out string failureReason);
    bool TryGetActor(string persistentId, out CharacterActor actor);
    IReadOnlyList<EnemyIndividualSaveData> GetEnemyIndividuals();
    OffenseBattlePersistenceState CapturePersistentState();
    bool TryRestoreBattle(
        OffenseExpeditionRun expedition,
        OffenseBattlePersistenceState state,
        out string message);
    void ClearForPersistentRestore();
    void SetBattleViewVisible(bool visible);
    void ClearCompletedBattle();
}

/// <summary>
/// Read-only production evidence for the enemy tactical decision pipeline.
/// The battle runtime owns this trace so a verifier does not have to invoke
/// the tactical service or session command executor directly.
/// </summary>
public interface IOffenseEnemyTacticalTraceQuery
{
    IReadOnlyList<OffenseEnemyTacticalExecutionTrace> EnemyTacticalTrace { get; }
}

public readonly struct OffenseEnemyTacticalExecutionTrace
{
    public OffenseEnemyTacticalExecutionTrace(
        long sequence,
        string battleId,
        string actorId,
        EnemyTacticalIntentKind intent,
        OffenseBattleActionType command,
        long commandId,
        string targetId,
        string abilityId,
        bool accepted,
        string terminalMessage)
    {
        Sequence = sequence;
        BattleId = battleId ?? string.Empty;
        ActorId = actorId ?? string.Empty;
        Intent = intent;
        Command = command;
        CommandId = commandId;
        TargetId = targetId ?? string.Empty;
        AbilityId = abilityId ?? string.Empty;
        Accepted = accepted;
        TerminalMessage = terminalMessage ?? string.Empty;
    }

    public long Sequence { get; }
    public string BattleId { get; }
    public string ActorId { get; }
    public EnemyTacticalIntentKind Intent { get; }
    public OffenseBattleActionType Command { get; }
    public long CommandId { get; }
    public string TargetId { get; }
    public string AbilityId { get; }
    public bool Accepted { get; }
    public string TerminalMessage { get; }
}

public sealed class OffenseBattleRestoreCandidate
{
    internal OffenseBattleRestoreCandidate(
        OffenseBattleSession session,
        Dictionary<string, CharacterActor> actorsById,
        bool isBattleViewVisible,
        string encounterId,
        IEnumerable<EnemyIndividualSaveData> enemyIndividuals)
    {
        Session = session;
        ActorsById = actorsById
            ?? throw new ArgumentNullException(nameof(actorsById));
        IsBattleViewVisible = isBattleViewVisible;
        EncounterId = encounterId?.Trim() ?? string.Empty;
        EnemyIndividuals = (enemyIndividuals ?? Array.Empty<EnemyIndividualSaveData>())
            .Select(value => value.Clone())
            .ToList();
    }

    internal OffenseBattleSession Session { get; }
    internal Dictionary<string, CharacterActor> ActorsById { get; }
    internal bool IsBattleViewVisible { get; }
    internal string EncounterId { get; }
    internal IReadOnlyList<EnemyIndividualSaveData> EnemyIndividuals { get; }
}

public sealed class OffenseBattleRuntime :
    IOffenseBattleRuntime,
    IOffenseEnemyTacticalTraceQuery,
    IStartable,
    IDisposable
{
    private readonly ICharacterWorldSaveService characterSaveService;
    private readonly RunVariableRuntime runVariables;
    private readonly ICombatResolutionService combatResolution;
    private readonly ICombatEquipmentRuntime combatEquipmentRuntime;
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly IOffenseRegionRuntime offenseRegionRuntime;
    private readonly IEnemyEncounterFactory enemyEncounterFactory;
    private readonly IEnemyTacticalDecisionService enemyTactics;
    private readonly IOffenseReturnArrivalRuntime returnArrivals;
    private readonly ICharacterPerformanceQuery performance;
    private readonly IGameEventBus gameEventBus;
    private ICharacterProficiencyCommand proficiencyCommands;
    private IGameCalendar proficiencyCalendar;
    private Dictionary<string, CharacterActor> actorsById =
        new Dictionary<string, CharacterActor>(StringComparer.Ordinal);
    private bool started;
    private bool completionRaised;
    private IDisposable ownerRunEndedSubscription;
    private string activeEncounterId = string.Empty;
    private List<EnemyIndividualSaveData> activeEnemyIndividuals = new();
    private readonly List<OffenseEnemyTacticalExecutionTrace> enemyTacticalTrace =
        new List<OffenseEnemyTacticalExecutionTrace>(64);
    private long enemyTacticalTraceSequence;

    public OffenseBattleRuntime(
        ICharacterWorldSaveService characterSaveService,
        DungeonSceneRuntimeReferences sceneRuntimes,
        IGameEventBus gameEventBus,
        ICombatResolutionService combatResolution,
        ICombatEquipmentRuntime combatEquipmentRuntime,
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterBodyHealthCommand bodyHealthCommands,
        IOffenseRegionRuntime offenseRegionRuntime,
        IEnemyEncounterFactory enemyEncounterFactory,
        IEnemyTacticalDecisionService enemyTactics,
        IOffenseReturnArrivalRuntime returnArrivals,
        ICharacterPerformanceQuery performance = null)
    {
        this.characterSaveService = characterSaveService
            ?? throw new ArgumentNullException(nameof(characterSaveService));
        runVariables = (sceneRuntimes
                ?? throw new ArgumentNullException(nameof(sceneRuntimes)))
            .RunVariables
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseBattleRuntime)} requires a loaded {nameof(RunVariableRuntime)}.");
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.combatResolution = combatResolution;
        this.combatEquipmentRuntime = combatEquipmentRuntime;
        this.bodyHealthQuery = bodyHealthQuery;
        this.bodyHealthCommands = bodyHealthCommands;
        this.offenseRegionRuntime = offenseRegionRuntime;
        this.enemyEncounterFactory = enemyEncounterFactory
            ?? throw new ArgumentNullException(nameof(enemyEncounterFactory));
        this.enemyTactics = enemyTactics
            ?? throw new ArgumentNullException(nameof(enemyTactics));
        this.returnArrivals = returnArrivals
            ?? throw new ArgumentNullException(nameof(returnArrivals));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
    }

    [Inject]
    public void ConstructProficiencyProgression(
        ICharacterProficiencyCommand commands,
        IGameCalendar calendar)
    {
        proficiencyCommands = commands;
        proficiencyCalendar = calendar;
    }

    public OffenseBattleSession Session { get; private set; }
    public bool HasActiveBattle => Session != null && !Session.IsComplete;
    public bool IsBattleViewVisible { get; private set; }
    public IReadOnlyList<OffenseEnemyTacticalExecutionTrace> EnemyTacticalTrace =>
        enemyTacticalTrace;
    public event Action StateChanged;
    public event Action<OffenseBattleSession> BattleCompleted;

    public void Start()
    {
        if (started) return;
        started = true;
        ownerRunEndedSubscription = gameEventBus.Subscribe<OwnerRunEndedEvent>(OnTriggerEvent);
    }

    public void Dispose()
    {
        if (!started) return;
        ownerRunEndedSubscription?.Dispose();
        ownerRunEndedSubscription = null;
        started = false;
    }

    public bool TryStartBattle(OffenseExpeditionRun expedition, out string message)
    {
        if (expedition == null || expedition.Target == null)
        {
            message = "원정 정보가 없습니다.";
            return false;
        }

        if (HasActiveBattle)
        {
            message = "이미 진행 중인 전투가 있습니다.";
            return false;
        }

        ResetEnemyTacticalTrace();

        actorsById.Clear();
        List<OffenseBattleCombatant> combatants = new List<OffenseBattleCombatant>();
        foreach (OffenseExpeditionMemberState member in expedition.MemberStates
            .Where(member => member?.Actor != null)
            .OrderBy(member => member.Formation)
            .Take(5))
        {
            CharacterActor actor = member.Actor;
            string persistentId = characterSaveService.GetOrAssignPersistentId(actor);
            actorsById[persistentId] = actor;
            OffenseBattleCombatant combatant = OffenseEncounterCatalog.CreateAlly(
                actor,
                persistentId,
                performance,
                member.Formation,
                member.Stress);
            ConfigureCombatEquipment(combatant);
            ConfigureBodyHealth(actor, combatant);
            combatants.Add(combatant);
        }

        if (combatants.Count == 0)
        {
            message = "전투에 참가할 원정대가 없습니다.";
            return false;
        }

        DungeonDifficulty difficulty = ResolveDifficulty();
        EnemyEncounterComposition encounter = enemyEncounterFactory.Create(
            expedition.Target,
            difficulty,
            expedition.ExpeditionId,
            expedition.Phase == OffenseExpeditionPhase.InBattle ? expedition.CurrentNode : null,
            offenseRegionRuntime?.GetPressureForTarget(expedition.Target) ?? default);
        combatants.AddRange(encounter.Combatants);
        activeEncounterId = encounter.Encounter.encounterId;
        activeEnemyIndividuals = encounter.Individuals
            .Select(value => value.Clone())
            .ToList();
        Session = new OffenseBattleSession(
            Guid.NewGuid().ToString("N"),
            expedition.ExpeditionId,
            expedition.Target.id,
            expedition.Target.title,
            difficulty,
            combatants,
            combatResolution,
            combatEquipmentRuntime,
            encounter.Rules);
        completionRaised = false;
        IsBattleViewVisible = true;
        TriggerBattleStarted(Session);
        StateChanged?.Invoke();
        message = $"{expedition.Target.title} 전투가 시작되었습니다.";
        return true;
    }

    public void AdvanceToPlayerDecision()
    {
        RunEnemyTurns();
    }

    public bool TryIssuePlayerCommand(
        OffenseBattleActionType actionType,
        string targetId,
        string abilityId,
        out OffenseBattleCommandResult result)
    {
        if (Session?.CurrentActor == null || Session.CurrentActor.Team != OffenseBattleTeam.Allies)
        {
            result = new OffenseBattleCommandResult(false, "현재 아군의 차례가 아닙니다.");
            return false;
        }

        OffenseBattleCommand command = new OffenseBattleCommand(
            Session.LastProcessedCommandId + 1,
            Session.CurrentActor.PersistentId,
            actionType,
            targetId,
            abilityId);
        return TryExecuteCommand(command, out result);
    }

    public bool TryExecuteCommand(
        OffenseBattleCommand command,
        out OffenseBattleCommandResult result)
    {
        if (Session == null)
        {
            result = new OffenseBattleCommandResult(false, "진행 중인 전투가 없습니다.");
            return false;
        }

        bool accepted = TryExecuteSessionCommand(command, out result);
        if (!accepted) return false;

        StateChanged?.Invoke();
        if (!RaiseCompletionIfNeeded()) RunEnemyTurns();
        return true;
    }

    public bool TryExecutePlannedCommand(
        int directorTurn,
        string actorId,
        string targetId,
        OffenseBattleActionType actionType,
        string abilityId,
        out OffenseBattleCommandResult result)
    {
        if (Session == null)
        {
            result = new OffenseBattleCommandResult(
                false,
                "진행 중인 오펜스 전투가 없습니다.");
            return false;
        }

        if (!Session.PreparePlannedRound(
                directorTurn,
                out string preparationFailure))
        {
            result = new OffenseBattleCommandResult(false, preparationFailure);
            return false;
        }

        if (actionType == OffenseBattleActionType.Ability
            && string.IsNullOrWhiteSpace(abilityId))
        {
            result = new OffenseBattleCommandResult(
                false,
                "Strategic Ability command is missing its source ability ID.");
            return false;
        }
        OffenseBattleCommand command = new OffenseBattleCommand(
            Session.LastProcessedCommandId + 1,
            actorId,
            actionType,
            targetId,
            actionType == OffenseBattleActionType.Ability ? abilityId : string.Empty);
        OffenseBattleCombatant actingBefore = Session.FindCombatant(actorId);
        OffenseBattleCombatant targetBefore = Session.FindCombatant(targetId);
        float targetHealthBefore = targetBefore?.CurrentHealth ?? 0f;
        bool targetWasDead = targetBefore?.IsDead ?? false;
        bool accepted = Session.TryExecutePlannedCommand(command, out result);
        if (!accepted)
        {
            return false;
        }

        AwardCombatCommand(
            command,
            actingBefore,
            result,
            Session.FindCombatant(targetId),
            targetHealthBefore,
            targetWasDead);
        StateChanged?.Invoke();
        return true;
    }

    public bool FinalizePlannedTurn(int directorTurn, out string failureReason)
    {
        if (Session == null)
        {
            failureReason = "There is no active battle session to finalize.";
            return false;
        }

        if (!Session.FinalizePlannedRound(directorTurn, out failureReason))
        {
            return false;
        }

        StateChanged?.Invoke();
        RaiseCompletionIfNeeded();
        failureReason = string.Empty;
        return true;
    }

    public bool TryGetActor(string persistentId, out CharacterActor actor)
    {
        actor = null;
        return !string.IsNullOrWhiteSpace(persistentId)
            && actorsById.TryGetValue(persistentId, out actor)
            && actor != null;
    }

    public IReadOnlyList<EnemyIndividualSaveData> GetEnemyIndividuals() =>
        activeEnemyIndividuals
            .Select(value => value.Clone())
            .ToArray();

    public OffenseBattlePersistenceState CapturePersistentState()
    {
        if (!HasActiveBattle) return null;
        OffenseBattlePersistenceState state = Session.CapturePersistentState();
        state.encounterId = activeEncounterId;
        state.enemyIndividuals = activeEnemyIndividuals
            .Select(value => value.Clone())
            .ToList();
        return state;
    }

    public bool TryRestoreBattle(
        OffenseExpeditionRun expedition,
        OffenseBattlePersistenceState state,
        out string message)
    {
        if (expedition == null || expedition.Target == null || state == null)
        {
            message = "복원할 전투 정보가 없습니다.";
            return false;
        }

        if (!string.Equals(expedition.ExpeditionId, state.expeditionId, StringComparison.Ordinal)
            || !string.Equals(expedition.Target.id, state.targetId, StringComparison.Ordinal))
        {
            message = "전투와 원정 식별자가 일치하지 않습니다.";
            return false;
        }

        actorsById.Clear();
        List<OffenseBattleCombatant> combatants = new List<OffenseBattleCombatant>();
        foreach (OffenseExpeditionMemberState member in expedition.MemberStates
            .Where(member => member?.Actor != null)
            .OrderBy(member => member.Formation)
            .Take(5))
        {
            CharacterActor actor = member.Actor;
            string persistentId = characterSaveService.GetOrAssignPersistentId(actor);
            actorsById[persistentId] = actor;
            OffenseBattleCombatant combatant = OffenseEncounterCatalog.CreateAlly(
                actor,
                persistentId,
                performance,
                member.Formation,
                member.Stress);
            ConfigureCombatEquipment(combatant);
            ConfigureBodyHealth(actor, combatant);
            combatants.Add(combatant);
        }

        EnemyEncounterComposition encounter = enemyEncounterFactory.Restore(
            state.encounterId,
            state.enemyIndividuals,
            state.difficulty,
            expedition.Phase == OffenseExpeditionPhase.InBattle ? expedition.CurrentNode : null,
            offenseRegionRuntime?.GetPressureForTarget(expedition.Target) ?? default);
        combatants.AddRange(encounter.Combatants);
        HashSet<string> configuredIds = combatants
            .Select(combatant => combatant.PersistentId)
            .ToHashSet(StringComparer.Ordinal);
        string missingId = (state.combatants ?? new List<OffenseBattleCombatantPersistenceState>())
            .Where(value => value != null)
            .Select(value => value.persistentId)
            .FirstOrDefault(id => !configuredIds.Contains(id));
        if (!string.IsNullOrWhiteSpace(missingId))
        {
            message = $"전투 참가자 '{missingId}'를 복원할 수 없습니다.";
            return false;
        }

        Session = OffenseBattleSession.Restore(
            state,
            combatants,
            combatResolution,
            combatEquipmentRuntime,
            encounter.Rules);
        activeEncounterId = encounter.Encounter.encounterId;
        activeEnemyIndividuals = encounter.Individuals
            .Select(value => value.Clone())
            .ToList();
        completionRaised = false;
        IsBattleViewVisible = true;
        StateChanged?.Invoke();
        if (!RaiseCompletionIfNeeded()) RunEnemyTurns();
        message = "전투를 현재 턴에서 복원했습니다.";
        return true;
    }

    internal OffenseBattleRestoreCandidate PreparePersistentRestore(
        OffenseExpeditionRun expedition,
        OffenseBattlePersistenceState state,
        OffenseStrategicPressureSnapshot? restoredPressure)
    {
        if (expedition == null || expedition.Target == null || state == null
            || !string.Equals(expedition.ExpeditionId, state.expeditionId,
                StringComparison.Ordinal)
            || !string.Equals(expedition.Target.id, state.targetId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Offense battle does not match its restored expedition.");
        }

        Dictionary<string, CharacterActor> candidateActors =
            new Dictionary<string, CharacterActor>(StringComparer.Ordinal);
        List<OffenseBattleCombatant> combatants =
            new List<OffenseBattleCombatant>();
        foreach (OffenseExpeditionMemberState member in expedition.MemberStates
                     .OrderBy(member => member.Formation))
        {
            CharacterActor actor = member?.Actor
                ?? throw new InvalidOperationException(
                    $"Expedition '{expedition.ExpeditionId}' has a null battle member.");
            string persistentId = characterSaveService.GetOrAssignPersistentId(actor);
            if (!candidateActors.TryAdd(persistentId, actor))
            {
                throw new InvalidOperationException(
                    $"Offense battle contains duplicate actor '{persistentId}'.");
            }
            OffenseBattleCombatant combatant = OffenseEncounterCatalog.CreateAlly(
                actor,
                persistentId,
                performance,
                member.Formation,
                member.Stress);
            ConfigureCombatEquipment(combatant);
            ConfigureBodyHealth(actor, combatant);
            combatants.Add(combatant);
        }

        EnemyEncounterComposition encounter = enemyEncounterFactory.Restore(
            state.encounterId,
            state.enemyIndividuals,
            state.difficulty,
            expedition.Phase == OffenseExpeditionPhase.InBattle
                ? expedition.CurrentNode
                : null,
            restoredPressure
                ?? offenseRegionRuntime?.GetPressureForTarget(expedition.Target)
                ?? default);
        combatants.AddRange(encounter.Combatants);
        HashSet<string> configuredIds = combatants
            .Select(combatant => combatant.PersistentId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> savedIds = state.combatants
            .Select(value => value.persistentId)
            .ToHashSet(StringComparer.Ordinal);
        string missingConfiguredId = savedIds
            .FirstOrDefault(id => !configuredIds.Contains(id));
        string missingSavedId = configuredIds
            .FirstOrDefault(id => !savedIds.Contains(id));
        if (!string.IsNullOrEmpty(missingConfiguredId)
            || !string.IsNullOrEmpty(missingSavedId))
        {
            throw new InvalidOperationException(
                $"Offense battle combatants do not match the configured encounter (unconfigured='{missingConfiguredId}', unsaved='{missingSavedId}').");
        }

        OffenseBattleSession candidateSession = OffenseBattleSession.Restore(
            state,
            combatants,
            combatResolution,
            combatEquipmentRuntime,
            encounter.Rules);
        return new OffenseBattleRestoreCandidate(
            candidateSession,
            candidateActors,
            isBattleViewVisible: true,
            encounterId: encounter.Encounter.encounterId,
            enemyIndividuals: encounter.Individuals);
    }

    internal OffenseBattleRestoreCandidate PrepareEmptyPersistentRestore() =>
        new OffenseBattleRestoreCandidate(
            session: null,
            new Dictionary<string, CharacterActor>(StringComparer.Ordinal),
            isBattleViewVisible: false,
            encounterId: string.Empty,
            enemyIndividuals: Array.Empty<EnemyIndividualSaveData>());

    internal void PublishPersistentRestore(
        OffenseBattleRestoreCandidate candidate)
    {
        candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        Session = candidate.Session;
        actorsById = candidate.ActorsById;
        activeEncounterId = candidate.EncounterId;
        activeEnemyIndividuals = candidate.EnemyIndividuals
            .Select(value => value.Clone())
            .ToList();
        completionRaised = false;
        IsBattleViewVisible = candidate.IsBattleViewVisible;
    }

    public void ClearForPersistentRestore()
    {
        Session = null;
        actorsById.Clear();
        activeEncounterId = string.Empty;
        activeEnemyIndividuals.Clear();
        completionRaised = false;
        IsBattleViewVisible = false;
        StateChanged?.Invoke();
    }

    public void SetBattleViewVisible(bool visible)
    {
        if (Session == null || IsBattleViewVisible == visible) return;
        IsBattleViewVisible = visible;
        StateChanged?.Invoke();
    }

    public void ClearCompletedBattle()
    {
        if (Session == null || !Session.IsComplete) return;
        Session = null;
        actorsById.Clear();
        activeEncounterId = string.Empty;
        activeEnemyIndividuals.Clear();
        completionRaised = false;
        IsBattleViewVisible = false;
        StateChanged?.Invoke();
    }

    public void OnTriggerEvent(OwnerRunEndedEvent eventType)
    {
        if (!HasActiveBattle) return;
        Session.AbortForOwnerDeath();
        StateChanged?.Invoke();
        RaiseCompletionIfNeeded();
    }

    private void RunEnemyTurns()
    {
        int safety = 0;
        while (Session != null
            && !Session.IsComplete
            && Session.CurrentActor != null
            && Session.CurrentActor.Team == OffenseBattleTeam.Enemies
            && safety++ < 100)
        {
            EnemyIndividualSaveData individual = activeEnemyIndividuals
                .FirstOrDefault(value => string.Equals(
                    value.characterId,
                    Session.CurrentActor.PersistentId,
                    StringComparison.Ordinal));
            if (individual == null)
                throw new InvalidOperationException(
                    $"Enemy combatant '{Session.CurrentActor.PersistentId}' has no persistent individual profile.");
            EnemyTacticalDecision decision = enemyTactics.Decide(
                Session,
                individual);
            OffenseBattleCommand command = ToCommand(decision);
            bool accepted = TryExecuteSessionCommand(
                command,
                out OffenseBattleCommandResult result);
            RecordEnemyTacticalTrace(decision, command, accepted, result);
            if (!accepted)
            {
                decision = enemyTactics.Decide(
                    Session,
                    individual,
                    allowAbility: false);
                command = ToCommand(decision);
                accepted = TryExecuteSessionCommand(
                    command,
                    out result);
                RecordEnemyTacticalTrace(decision, command, accepted, result);
                if (!accepted) break;
            }
            StateChanged?.Invoke();
        }

        RaiseCompletionIfNeeded();
    }

    private void ResetEnemyTacticalTrace()
    {
        enemyTacticalTrace.Clear();
        enemyTacticalTraceSequence = 0;
    }

    private void RecordEnemyTacticalTrace(
        EnemyTacticalDecision decision,
        OffenseBattleCommand command,
        bool accepted,
        OffenseBattleCommandResult result)
    {
        const int capacity = 256;
        if (enemyTacticalTrace.Count >= capacity)
        {
            enemyTacticalTrace.RemoveAt(0);
        }

        enemyTacticalTrace.Add(new OffenseEnemyTacticalExecutionTrace(
            ++enemyTacticalTraceSequence,
            Session?.BattleId,
            command?.ActorId,
            decision.Intent,
            command?.ActionType ?? OffenseBattleActionType.BasicAttack,
            command?.CommandId ?? 0,
            command?.TargetId,
            command?.AbilityId,
            accepted,
            result?.Message));
    }

    private OffenseBattleCommand ToCommand(EnemyTacticalDecision decision)
    {
        OffenseBattleActionType action = decision.Intent switch
        {
            EnemyTacticalIntentKind.UseAbility => OffenseBattleActionType.Ability,
            EnemyTacticalIntentKind.Retreat => OffenseBattleActionType.Retreat,
            EnemyTacticalIntentKind.Protect => OffenseBattleActionType.Guard,
            EnemyTacticalIntentKind.Move => OffenseBattleActionType.Advance,
            _ => OffenseBattleActionType.BasicAttack
        };
        return new OffenseBattleCommand(
            Session.LastProcessedCommandId + 1,
            Session.CurrentActor.PersistentId,
            action,
            decision.TargetId,
            decision.AbilityId);
    }

    private bool RaiseCompletionIfNeeded()
    {
        if (Session == null || !Session.IsComplete || completionRaised) return false;
        if (Session.Outcome == OffenseBattleOutcome.Victory)
        {
            returnArrivals.RegisterBattlePrisonerCandidates(
                Session.ExpeditionId,
                activeEnemyIndividuals);
        }
        else
        {
            returnArrivals.DiscardBattlePrisonerCandidates(
                Session.ExpeditionId);
        }
        if (proficiencyCommands != null && proficiencyCalendar != null)
        {
            foreach (OffenseBattleCombatant combatant in Session.Combatants)
            {
                if (combatant == null || combatant.IsDead
                    || combatant.Team != OffenseBattleTeam.Allies
                    || !TryGetActor(combatant.PersistentId, out CharacterActor actor)
                    || !CharacterPersistentIdentity.TryGet(
                        actor,
                        out CharacterId characterId))
                {
                    continue;
                }

                CharacterProficiencyId proficiencyId =
                    combatant.Weapon?.IsRanged == true
                        ? BuiltInCharacterProficiencyIds.RangedCombat
                        : BuiltInCharacterProficiencyIds.MeleeCombat;
                proficiencyCommands.AddCombatExperience(
                    characterId,
                    proficiencyId,
                    0.50f,
                    training: false,
                    stableAwardKey:
                        $"{Session.BattleId}:{combatant.PersistentId}:complete",
                    absoluteHour: proficiencyCalendar.AbsoluteHour);
            }
        }
        completionRaised = true;
        SynchronizeAlliedBodyHealth();
        BattleCompleted?.Invoke(Session);
        return true;
    }

    private bool TryExecuteSessionCommand(
        OffenseBattleCommand command,
        out OffenseBattleCommandResult result)
    {
        OffenseBattleCombatant actingCombatant = Session?.CurrentActor;
        OffenseBattleCombatant targetBefore = command != null
            ? Session?.FindCombatant(command.TargetId)
            : null;
        float targetHealthBefore = targetBefore?.CurrentHealth ?? 0f;
        bool targetWasDead = targetBefore?.IsDead ?? false;
        CharacterActor actingActor = actingCombatant != null
            && actingCombatant.Team == OffenseBattleTeam.Allies
            && TryGetActor(actingCombatant.PersistentId, out CharacterActor resolvedActor)
                ? resolvedActor
                : null;
        bool offenseUltimateCommand = IsOffenseUltimateCommand(actingActor, command);
        int ultimateBattleSerial = Session != null
            ? CharacterGrowthRules.StableHash(Session.BattleId)
            : 0;
        if (offenseUltimateCommand
            && !actingActor.Progression.CanUseUltimate(CharacterUltimateDomain.Offense, ultimateBattleSerial))
        {
            result = new OffenseBattleCommandResult(false, "이미 이 전투에서 궁극기를 사용했습니다.");
            return false;
        }

        bool accepted = Session.TryExecuteCommand(command, out result);
        if (!accepted)
        {
            return false;
        }

        AwardCombatCommand(
            command,
            actingCombatant,
            result,
            targetAfter: command != null
                ? Session.FindCombatant(command.TargetId)
                : targetBefore,
            targetHealthBefore,
            targetWasDead);
        AwardDefensiveCombat(
            command,
            actingCombatant,
            targetAfter: command != null
                ? Session.FindCombatant(command.TargetId)
                : targetBefore,
            result);

        if (offenseUltimateCommand)
        {
            actingActor.Progression.TryMarkUltimateUsed(
                CharacterUltimateDomain.Offense,
                ultimateBattleSerial);
        }

        OffenseBattleCombatant targetAfter = command != null
            ? Session.FindCombatant(command.TargetId)
            : targetBefore;
        if (targetAfter != null
            && targetAfter.Team == OffenseBattleTeam.Allies
            && targetAfter.CurrentHealth < targetHealthBefore
            && TryGetActor(targetAfter.PersistentId, out CharacterActor damagedActor))
        {
            CharacterSkillRuntimeEffects.ApplyTriggeredPassives(new CharacterSkillExecutionContext(
                damagedActor,
                CharacterSkillTrigger.DamageTaken,
                $"{Session.BattleId}:{command.CommandId}:damage-taken",
                Session,
                targetAfter,
                actingCombatant));
        }

        if (actingCombatant != null
            && actingCombatant.Team == OffenseBattleTeam.Allies
            && targetAfter != null
            && targetAfter.Team == OffenseBattleTeam.Enemies
            && !targetWasDead
            && targetAfter.IsDead
            && TryGetActor(actingCombatant.PersistentId, out CharacterActor attacker))
        {
            CharacterSkillRuntimeEffects.ApplyTriggeredPassives(new CharacterSkillExecutionContext(
                attacker,
                CharacterSkillTrigger.EnemyDefeated,
                $"{Session.BattleId}:{command.CommandId}:enemy-defeated",
                Session,
                actingCombatant,
                targetAfter));
        }

        return true;
    }

    private void AwardCombatCommand(
        OffenseBattleCommand command,
        OffenseBattleCombatant actingCombatant,
        OffenseBattleCommandResult commandResult,
        OffenseBattleCombatant targetAfter,
        float targetHealthBefore,
        bool targetWasDead)
    {
        if (proficiencyCommands == null || proficiencyCalendar == null
            || command == null || actingCombatant == null
            || actingCombatant.Team != OffenseBattleTeam.Allies
            || targetAfter == null
            || targetAfter.Team != OffenseBattleTeam.Enemies
            || targetWasDead
            || (command.ActionType != OffenseBattleActionType.BasicAttack
                && command.ActionType != OffenseBattleActionType.Ability)
            || !TryGetActor(actingCombatant.PersistentId, out CharacterActor actor)
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
        {
            return;
        }

        float damage = Math.Max(0f, targetHealthBefore - targetAfter.CurrentHealth);
        if (commandResult?.Hit == true && damage <= 0f)
        {
            return;
        }
        float experience = 0.20f;
        if (damage > 0f)
        {
            experience += 0.15f;
            experience += Math.Min(0.35f, damage * 0.01f);
        }
        CharacterProficiencyId proficiencyId =
            actingCombatant.Weapon?.IsRanged == true
                ? BuiltInCharacterProficiencyIds.RangedCombat
                : BuiltInCharacterProficiencyIds.MeleeCombat;
        proficiencyCommands.AddCombatExperience(
            characterId,
            proficiencyId,
            experience,
            training: false,
            stableAwardKey: $"{Session.BattleId}:{command.CommandId}:attack",
            absoluteHour: proficiencyCalendar.AbsoluteHour);
    }

    private void AwardDefensiveCombat(
        OffenseBattleCommand command,
        OffenseBattleCombatant actingCombatant,
        OffenseBattleCombatant targetAfter,
        OffenseBattleCommandResult commandResult)
    {
        if (proficiencyCommands == null || proficiencyCalendar == null
            || command == null || commandResult == null
            || (!commandResult.ShieldBlocked && !commandResult.CoverBlocked)
            || actingCombatant == null
            || actingCombatant.Team != OffenseBattleTeam.Enemies
            || targetAfter == null
            || targetAfter.Team != OffenseBattleTeam.Allies
            || !TryGetActor(targetAfter.PersistentId, out CharacterActor defender)
            || !CharacterPersistentIdentity.TryGet(
                defender,
                out CharacterId characterId))
        {
            return;
        }
        CharacterProficiencyId proficiencyId =
            targetAfter.Weapon?.IsRanged == true
                ? BuiltInCharacterProficiencyIds.RangedCombat
                : BuiltInCharacterProficiencyIds.MeleeCombat;
        proficiencyCommands.AddCombatExperience(
            characterId,
            proficiencyId,
            0.25f,
            training: false,
            stableAwardKey: $"{Session.BattleId}:{command.CommandId}:block:{targetAfter.PersistentId}",
            absoluteHour: proficiencyCalendar.AbsoluteHour);
    }

    private void ConfigureCombatEquipment(OffenseBattleCombatant combatant)
    {
        if (combatant == null || combatEquipmentRuntime == null)
        {
            return;
        }

        combatEquipmentRuntime.TryGetActiveWeapon(
            combatant.PersistentId,
            out CombatWeaponSnapshot weapon);
        combatant.SetCombatEquipment(
            weapon,
            combatEquipmentRuntime.GetArmor(combatant.PersistentId),
            combatEquipmentRuntime.GetShield(combatant.PersistentId));
    }

    private void ConfigureBodyHealth(CharacterActor actor, OffenseBattleCombatant combatant)
    {
        if (actor == null || combatant == null || bodyHealthQuery == null)
        {
            return;
        }

        combatant.ApplyBodyHealth(bodyHealthQuery.GetSnapshot(actor));
        CharacterPerformanceSnapshot arcane = performance.Evaluate(
            actor,
            CharacterPerformanceFormulaIds.ArcanePower);
        if (!arcane.IsApplicable)
        {
            throw new InvalidOperationException(
                arcane.Failure?.Message
                ?? $"Arcane power is unavailable for battle combatant '{combatant.PersistentId}'.");
        }
        combatant.SetArcanePowerMultiplier(arcane.Value);
    }

    private void SynchronizeAlliedBodyHealth()
    {
        if (Session == null || bodyHealthCommands == null)
        {
            return;
        }

        foreach (OffenseBattleCombatant combatant in Session.Combatants
            .Where(value => value.Team == OffenseBattleTeam.Allies))
        {
            if (TryGetActor(combatant.PersistentId, out CharacterActor actor))
            {
                bodyHealthCommands.ApplySnapshot(
                    actor,
                    combatant.CaptureBodyHealth(),
                    "원정 전투 부상");
            }
        }
    }

    private static bool IsOffenseUltimateCommand(CharacterActor actor, OffenseBattleCommand command)
    {
        CharacterSkillInstance ultimate = actor?.Progression?.Ultimate;
        return command != null
            && command.ActionType == OffenseBattleActionType.Ability
            && ultimate != null
            && ultimate.ultimateDomain == CharacterUltimateDomain.Offense
            && string.Equals(command.AbilityId, ultimate.id, StringComparison.Ordinal);
    }

    private void TriggerBattleStarted(OffenseBattleSession session)
    {
        if (session == null)
        {
            return;
        }

        foreach (OffenseBattleCombatant combatant in session.Combatants
            .Where(combatant => combatant.Team == OffenseBattleTeam.Allies))
        {
            if (!TryGetActor(combatant.PersistentId, out CharacterActor actor))
            {
                continue;
            }

            CharacterSkillRuntimeEffects.ApplyTriggeredPassives(new CharacterSkillExecutionContext(
                actor,
                CharacterSkillTrigger.BattleStarted,
                $"{session.BattleId}:battle-started",
                session,
                combatant,
                null));
        }
    }

    private DungeonDifficulty ResolveDifficulty()
    {
        if (runVariables.State.StartVariables != null)
        {
            return runVariables.State.StartVariables.runDifficulty;
        }

        return DungeonDifficulty.Normal;
    }
}
