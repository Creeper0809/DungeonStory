using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using VContainer;
using VContainer.Unity;

public interface IOffenseBattleRuntime
{
    OffenseBattleSnapshot Session { get; }
    bool HasActiveBattle { get; }
    bool IsBattleViewVisible { get; }
    event Action StateChanged;
    CombatAttackPreview PreviewBasicAttack(string sourceId, string targetId);
    OffenseBattleCommand CreateStrategicEnemyCommand(string enemyId, long commandId);
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

public interface IOffenseBattleCompletionAuthority : IOffenseBattleRuntime
{
    event Action<OffenseBattleSession> BattleCompleted;
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

public sealed class OffenseBattleSnapshot
{
    internal OffenseBattleSnapshot(OffenseBattleSession session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        BattleId = session.BattleId;
        ExpeditionId = session.ExpeditionId;
        TargetId = session.TargetId;
        TargetTitle = session.TargetTitle;
        Difficulty = session.Difficulty;
        Outcome = session.Outcome;
        EncounterRules = session.EncounterRules.CreateSnapshot();
        RoundNumber = session.RoundNumber;
        LastProcessedCommandId = session.LastProcessedCommandId;
        CurrentOrderIndex = session.CurrentOrderIndex;
        Combatants = Array.AsReadOnly(session.Combatants.Select(value => value.CreateSnapshot()).ToArray());
        InitiativeOrder = Array.AsReadOnly(session.InitiativeOrder.ToArray());
        Log = Array.AsReadOnly(session.Log.ToArray());
    }
    public string BattleId { get; }
    public string ExpeditionId { get; }
    public string TargetId { get; }
    public string TargetTitle { get; }
    public DungeonDifficulty Difficulty { get; }
    public OffenseBattleOutcome Outcome { get; }
    public OffenseBattleEncounterRules EncounterRules { get; }
    public int RoundNumber { get; }
    public long LastProcessedCommandId { get; }
    public int CurrentOrderIndex { get; }
    public IReadOnlyList<OffenseBattleCombatant> Combatants { get; }
    public IReadOnlyList<string> InitiativeOrder { get; }
    public IReadOnlyList<string> Log { get; }
    public bool IsComplete => Outcome != OffenseBattleOutcome.InProgress;
    public OffenseBattleCombatant CurrentActor => CurrentOrderIndex >= 0
        && CurrentOrderIndex < InitiativeOrder.Count
            ? FindCombatant(InitiativeOrder[CurrentOrderIndex]) : null;
    public OffenseBattleCombatant FindCombatant(string persistentId) =>
        Combatants.FirstOrDefault(value => string.Equals(value.PersistentId, persistentId, StringComparison.Ordinal));
}

public sealed class OffenseBattleRuntime :
    IOffenseBattleRuntime,
    IOffenseBattleCompletionAuthority,
    IOffenseEnemyTacticalTraceQuery,
    IDungeonRestoreTransactionParticipant,
    IStartable,
    ITickable,
    IDisposable
{
    private delegate bool BattleCommandExecutor(
        OffenseBattleCommand command,
        out OffenseBattleCommandResult result);

    private readonly ICharacterWorldSaveService characterSaveService;
    private readonly DungeonRestorePublication restorePublication = new();
    private readonly RunVariableRuntime runVariables;
    private readonly ICombatResolutionService combatResolution;
    private readonly IItemDefinitionCatalog itemCatalog;
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
    private OffenseBattleCommandPublicationRuntime commandPublications;
    private ICharacterSkillAppliedActivationCommand skillActivationTransactions;
    private bool restoring, recoveringCommand;
    private long nextRecoveryTick;
    private Dictionary<string, CharacterActor> actorsById =
        new Dictionary<string, CharacterActor>(StringComparer.Ordinal);
    private bool started;
    private bool completionRaised;
    private IDisposable ownerRunEndedSubscription;
    private string activeEncounterId = string.Empty;
    private List<EnemyIndividualSaveData> activeEnemyIndividuals = new();
    private List<OffenseEnemyTacticalExecutionTrace> enemyTacticalTrace =
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
        IItemDefinitionCatalog itemCatalog,
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
        this.itemCatalog = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
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
        IGameCalendar calendar,
        OffenseBattleCommandPublicationRuntime commandPublications)
    {
        proficiencyCommands = commands;
        proficiencyCalendar = calendar;
        this.commandPublications = commandPublications
            ?? throw new ArgumentNullException(nameof(commandPublications));
    }

    [Inject]
    public void ConstructSkillActivationRuntime(
        ICharacterSkillAppliedActivationCommand transactions)
    {
        skillActivationTransactions = transactions
            ?? throw new ArgumentNullException(nameof(transactions));
    }

    internal OffenseBattleSession OwnedSession { get; private set; }
    public OffenseBattleSnapshot Session => OwnedSession == null ? null : new OffenseBattleSnapshot(OwnedSession);
    public CombatAttackPreview PreviewBasicAttack(string sourceId, string targetId) =>
        OwnedSession != null
            ? OwnedSession.PreviewBasicAttack(OwnedSession.FindCombatant(sourceId), OwnedSession.FindCombatant(targetId))
            : new CombatAttackPreview(false, "진행 중인 전투가 없습니다.", CombatRangeBand.OutOfRange,
                0f, 0f, 0f, 0f, 0f, 0f);
    public OffenseBattleCommand CreateStrategicEnemyCommand(string enemyId, long commandId) =>
        OwnedSession?.CreateStrategicEnemyCommand(enemyId, commandId);
    public bool HasActiveBattle => OwnedSession != null && (!OwnedSession.IsComplete || HasPendingObservation);
    private bool HasPendingObservation => commandPublications?.HasPending(OwnedSession?.BattleId) == true;
    public bool IsBattleViewVisible { get; private set; }
    public IReadOnlyList<OffenseEnemyTacticalExecutionTrace> EnemyTacticalTrace =>
        enemyTacticalTrace;
    public event Action StateChanged;
    private event Action<OffenseBattleSession> battleCompleted;
    event Action<OffenseBattleSession> IOffenseBattleCompletionAuthority.BattleCompleted
    {
        add => battleCompleted += value;
        remove => battleCompleted -= value;
    }

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

        if (HasActiveBattle || commandPublications?.HasAnyPending == true)
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
        OwnedSession = new OffenseBattleSession(
            Guid.NewGuid().ToString("N"),
            expedition.ExpeditionId,
            expedition.Target.id,
            expedition.Target.title,
            difficulty,
            combatants,
            combatResolution,
            combatEquipmentRuntime,
            encounter.Rules);
        OwnedSession.BindSettlementOwner(expedition);
        completionRaised = false;
        IsBattleViewVisible = true;
        TriggerBattleStarted(OwnedSession);
        StateChanged?.Invoke();
        message = $"{expedition.Target.title} 전투가 시작되었습니다.";
        return true;
    }

    public void AdvanceToPlayerDecision()
    {
        RunEnemyTurns();
    }

    public void Tick()
    {
        if (restoring || !HasPendingObservation) return;
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (now < nextRecoveryTick) return;
        nextRecoveryTick = now + System.Diagnostics.Stopwatch.Frequency;
        if (!RecoverCommandObservation()) return;
        StateChanged?.Invoke();
        if (!RaiseCompletionIfNeeded()) RunEnemyTurns();
    }

    public bool TryIssuePlayerCommand(
        OffenseBattleActionType actionType,
        string targetId,
        string abilityId,
        out OffenseBattleCommandResult result)
    {
        if (OwnedSession?.CurrentActor == null || OwnedSession.CurrentActor.Team != OffenseBattleTeam.Allies)
        {
            result = new OffenseBattleCommandResult(false, "현재 아군의 차례가 아닙니다.");
            return false;
        }

        OffenseBattleCommand command = new OffenseBattleCommand(
            OwnedSession.LastProcessedCommandId + 1,
            OwnedSession.CurrentActor.PersistentId,
            actionType,
            targetId,
            abilityId);
        return TryExecuteCommand(command, out result);
    }

    public bool TryExecuteCommand(
        OffenseBattleCommand command,
        out OffenseBattleCommandResult result)
    {
        if (OwnedSession == null)
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
        if (OwnedSession == null)
        {
            result = new OffenseBattleCommandResult(
                false,
                "진행 중인 오펜스 전투가 없습니다.");
            return false;
        }

        if (!RecoverCommandObservation())
        { result = new OffenseBattleCommandResult(false, commandPublications.LastFailure); return false; }
        OffenseBattlePersistenceState commandBefore =
            OwnedSession.CapturePersistentState();
        if (!OwnedSession.PreparePlannedRound(
                directorTurn,
                out string preparationFailure))
        {
            RestoreRejectedBattleCommand(commandBefore);
            result = new OffenseBattleCommandResult(false, preparationFailure);
            return false;
        }

        if (actionType == OffenseBattleActionType.Ability
            && string.IsNullOrWhiteSpace(abilityId))
        {
            RestoreRejectedBattleCommand(commandBefore);
            result = new OffenseBattleCommandResult(
                false,
                "Strategic Ability command is missing its source ability ID.");
            return false;
        }
        OffenseBattleCommand command = new OffenseBattleCommand(
            OwnedSession.LastProcessedCommandId + 1,
            actorId,
            actionType,
            targetId,
            actionType is OffenseBattleActionType.Ability
                or OffenseBattleActionType.SwitchWeapon
                or OffenseBattleActionType.SetFireMode
                    ? abilityId
                    : string.Empty);
        OffenseBattleCombatant actingBefore = OwnedSession.FindCombatant(actorId);
        if (!TryExecuteCommandWithOutcome(
                command,
                actingBefore,
                OwnedSession.TryExecutePlannedCommand,
                commandBefore,
                out result))
        {
            return false;
        }
        StateChanged?.Invoke();
        return true;
    }

    public bool FinalizePlannedTurn(int directorTurn, out string failureReason)
    {
        if (OwnedSession == null)
        {
            failureReason = "There is no active battle session to finalize.";
            return false;
        }

        if (!RecoverCommandObservation())
        { failureReason = commandPublications.LastFailure; return false; }
        if (!OwnedSession.FinalizePlannedRound(directorTurn, out failureReason))
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
        OffenseBattlePersistenceState state = OwnedSession.CapturePersistentState();
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

        Dictionary<string, CharacterActor> candidateActors =
            new Dictionary<string, CharacterActor>(StringComparer.Ordinal);
        List<OffenseBattleCombatant> combatants = new List<OffenseBattleCombatant>();
        foreach (OffenseExpeditionMemberState member in expedition.MemberStates
            .Where(member => member?.Actor != null)
            .OrderBy(member => member.Formation)
            .Take(5))
        {
            CharacterActor actor = member.Actor;
            string persistentId = characterSaveService.GetOrAssignPersistentId(actor);
            if (!candidateActors.TryAdd(persistentId, actor))
                throw new InvalidOperationException(
                    $"Offense battle contains duplicate actor '{persistentId}'.");
            OffenseBattleCombatant combatant = OffenseEncounterCatalog.RestoreAlly(
                actor,
                persistentId,
                state.combatants.Single(value => value != null
                    && string.Equals(value.persistentId, persistentId, StringComparison.Ordinal)));
            ConfigureCombatEquipment(combatant);
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

        OffenseBattleSession candidateSession = OffenseBattleSession.Restore(
            state,
            combatants,
            combatResolution,
            combatEquipmentRuntime,
            encounter.Rules);
        candidateSession.BindSettlementOwner(expedition);
        List<EnemyIndividualSaveData> candidateEnemies = encounter.Individuals
            .Select(value => value.Clone())
            .ToList();
        actorsById = candidateActors;
        OwnedSession = candidateSession;
        activeEncounterId = encounter.Encounter.encounterId;
        activeEnemyIndividuals = candidateEnemies;
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
            OffenseBattleCombatant combatant = OffenseEncounterCatalog.RestoreAlly(
                actor,
                persistentId,
                state.combatants.Single(value => value != null
                    && string.Equals(value.persistentId, persistentId, StringComparison.Ordinal)));
            ConfigureCombatEquipment(combatant);
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
        candidateSession.BindSettlementOwner(expedition);
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
        if (restorePublication.TryStage(() => ApplyRestore(candidate), CaptureRestoreRollback)) return;
        ApplyRestore(candidate);
    }

    private Action CaptureRestoreRollback()
    {
        var previous0 = OwnedSession;
        var previous1 = actorsById;
        var previous2 = activeEncounterId;
        var previous3 = activeEnemyIndividuals;
        var previous4 = completionRaised;
        var previous5 = IsBattleViewVisible;
        var previousTrace = enemyTacticalTrace;
        long previousTraceSequence = enemyTacticalTraceSequence;
        return () =>
        {
            OwnedSession = previous0;
            actorsById = previous1;
            activeEncounterId = previous2;
            activeEnemyIndividuals = previous3;
            completionRaised = previous4;
            IsBattleViewVisible = previous5;
            enemyTacticalTrace = previousTrace;
            enemyTacticalTraceSequence = previousTraceSequence;
        };
    }

    public string ParticipantId => "offense.battle-runtime";
    public void BeginRestoreCandidate() { restorePublication.Begin(); restoring = true; }
    public void PublishRestoreCandidate() => restorePublication.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restorePublication.Discard(); } finally { restoring = false; } }
    public void CompleteRestoreCandidate() { restorePublication.Complete(); restoring = false; }

    private void ApplyRestore(OffenseBattleRestoreCandidate candidate)
    {
        OwnedSession = candidate.Session;
        actorsById = candidate.ActorsById;
        activeEncounterId = candidate.EncounterId;
        activeEnemyIndividuals = candidate.EnemyIndividuals
            .Select(value => value.Clone())
            .ToList();
        completionRaised = false;
        IsBattleViewVisible = candidate.IsBattleViewVisible;
        // Traces describe the previous live session, not the restored one.
        // Swap instead of Clear so a later participant failure can restore exact identity.
        enemyTacticalTrace = new List<OffenseEnemyTacticalExecutionTrace>(64);
        enemyTacticalTraceSequence = 0;
    }

    public void ClearForPersistentRestore()
    {
        OwnedSession = null;
        actorsById.Clear();
        activeEncounterId = string.Empty;
        activeEnemyIndividuals.Clear();
        completionRaised = false;
        IsBattleViewVisible = false;
        StateChanged?.Invoke();
    }

    public void SetBattleViewVisible(bool visible)
    {
        if (OwnedSession == null || IsBattleViewVisible == visible) return;
        IsBattleViewVisible = visible;
        StateChanged?.Invoke();
    }

    public void ClearCompletedBattle()
    {
        if (OwnedSession == null || !OwnedSession.IsComplete || HasPendingObservation) return;
        List<string> enemyCharacterIds = new List<string>();
        foreach (OffenseBattleCombatant combatant in OwnedSession.Combatants)
        {
            if (combatant == null)
            {
                throw new InvalidOperationException(
                    "A completed offense battle contains a null combatant.");
            }
            if (combatant.Team != OffenseBattleTeam.Enemies)
            {
                continue;
            }

            string characterId = combatant.PersistentId?.Trim()
                ?? string.Empty;
            if (characterId.Length == 0
                || !string.Equals(
                    combatant.PersistentId,
                    characterId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Completed offense battle enemy has invalid character ID "
                    + $"'{combatant.PersistentId ?? string.Empty}'.");
            }
            if (!enemyCharacterIds.Contains(characterId))
            {
                enemyCharacterIds.Add(characterId);
            }
        }

        foreach (string enemyCharacterId in enemyCharacterIds)
        {
            combatEquipmentRuntime.HandleCharacterDeath(enemyCharacterId);
        }
        OwnedSession = null;
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
        OwnedSession.AbortForOwnerDeath();
        StateChanged?.Invoke();
        RaiseCompletionIfNeeded();
    }

    private void RunEnemyTurns()
    {
        if (!RecoverCommandObservation()) return;
        int safety = 0;
        while (OwnedSession != null
            && !OwnedSession.IsComplete
            && !HasPendingObservation
            && OwnedSession.CurrentActor != null
            && OwnedSession.CurrentActor.Team == OffenseBattleTeam.Enemies
            && safety++ < 100)
        {
            EnemyIndividualSaveData individual = activeEnemyIndividuals
                .FirstOrDefault(value => string.Equals(
                    value.characterId,
                    OwnedSession.CurrentActor.PersistentId,
                    StringComparison.Ordinal));
            if (individual == null)
                throw new InvalidOperationException(
                    $"Enemy combatant '{OwnedSession.CurrentActor.PersistentId}' has no persistent individual profile.");
            EnemyTacticalDecision decision = enemyTactics.Decide(
                OwnedSession,
                individual);
            OffenseBattleCommand command = ToCommand(decision);
            bool accepted = TryExecuteSessionCommand(
                command,
                out OffenseBattleCommandResult result);
            RecordEnemyTacticalTrace(decision, command, accepted, result);
            if (!accepted && !HasPendingObservation)
            {
                decision = enemyTactics.Decide(
                    OwnedSession,
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
            OwnedSession?.BattleId,
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
            OwnedSession.LastProcessedCommandId + 1,
            OwnedSession.CurrentActor.PersistentId,
            action,
            decision.TargetId,
            decision.AbilityId);
    }

    private bool RaiseCompletionIfNeeded()
    {
        if (OwnedSession == null || !OwnedSession.IsComplete || completionRaised || HasPendingObservation) return false;
        if (OwnedSession.Outcome == OffenseBattleOutcome.Victory)
        {
            returnArrivals.RegisterBattlePrisonerCandidates(
                OwnedSession.ExpeditionId,
                activeEnemyIndividuals);
        }
        else
        {
            returnArrivals.DiscardBattlePrisonerCandidates(
                OwnedSession.ExpeditionId);
        }
        if (proficiencyCommands != null && proficiencyCalendar != null)
        {
            foreach (OffenseBattleCombatant combatant in OwnedSession.Combatants)
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
                        $"{OwnedSession.BattleId}:{combatant.PersistentId}:complete",
                    absoluteHour: proficiencyCalendar.AbsoluteHour);
            }
        }
        completionRaised = true;
        SynchronizeAlliedBodyHealth();
        battleCompleted?.Invoke(OwnedSession);
        return true;
    }

    private bool TryExecuteSessionCommand(
        OffenseBattleCommand command,
        out OffenseBattleCommandResult result)
    {
        if (!RecoverCommandObservation())
        { result = new OffenseBattleCommandResult(false, commandPublications.LastFailure); return false; }
        // A replay cannot apply effects. Let the session's existing identity
        // guard reject it before reserving an outcome or rebuilding a snapshot.
        if (command != null && command.CommandId <= OwnedSession.LastProcessedCommandId)
            return OwnedSession.TryExecuteCommand(command, out result);
        return TryExecuteCommandWithOutcome(
            command,
            OwnedSession?.CurrentActor,
            OwnedSession.TryExecuteCommand,
            null,
            out result);
    }

    private bool TryExecuteCommandWithOutcome(
        OffenseBattleCommand command,
        OffenseBattleCombatant actingCombatant,
        BattleCommandExecutor execute,
        OffenseBattlePersistenceState rollbackSnapshot,
        out OffenseBattleCommandResult result)
    {
        if (command == null || actingCombatant == null)
        {
            RestoreRejectedBattleCommand(rollbackSnapshot);
            result = new OffenseBattleCommandResult(false, "명령 또는 행동자가 없습니다."); return false;
        }
        if (commandPublications == null || proficiencyCalendar == null)
            throw new InvalidOperationException("battle-command-observation-unavailable");
        if (!RecoverCommandObservation())
        { result = new OffenseBattleCommandResult(false, commandPublications.LastFailure); return false; }

        var before = rollbackSnapshot ?? OwnedSession.CapturePersistentState();
        var target = OwnedSession.FindCombatant(command.TargetId);
        var data = new OffenseBattleCommandFactData
        {
            battleId = OwnedSession.BattleId, expeditionId = OwnedSession.ExpeditionId,
            commandId = command.CommandId, actorId = command.ActorId, actorDisplayName = actingCombatant.DisplayName ?? string.Empty,
            targetId = command.TargetId, targetDisplayName = target?.DisplayName ?? string.Empty,
            abilityId = command.AbilityId, actionType = command.ActionType,
            weaponInstanceId = actingCombatant.Weapon?.InstanceId ?? string.Empty,
            weaponDefinitionId = actingCombatant.Weapon?.DefinitionId ?? string.Empty,
            absoluteDay = Math.Max(1, proficiencyCalendar.Day), absoluteHour = Math.Max(0L, proficiencyCalendar.AbsoluteHour),
            targetWasDead = target?.IsDead ?? false, targetIsDead = target?.IsDead ?? false, targetHealthBefore = target?.CurrentHealth ?? 0f,
            targetHealthAfter = target?.CurrentHealth ?? 0f
        };
        try
        {
            if (!commandPublications.TryBegin(new OffenseBattleCommandFact(data), out string reserveFailure))
            {
                RestoreRejectedBattleCommand(before);
                result = new OffenseBattleCommandResult(false, reserveFailure); return false;
            }
        }
        catch { RestoreRejectedBattleCommand(before); throw; }

        CharacterSkillAppliedActivationScope scope = null;
        bool effectsRetained = false;
        try
        {
            CharacterActor actor = actingCombatant.Team == OffenseBattleTeam.Allies
                && TryGetActor(actingCombatant.PersistentId, out var found) ? found : null;
            bool ultimate = IsOffenseUltimateCommand(actor, command);
            if (ultimate)
            {
                if (skillActivationTransactions == null) throw new InvalidOperationException("skill-applied-command-unavailable");
                var request = new CharacterSkillAppliedActivationRequest(
                    new CharacterSkillExecutionContext(actor, CharacterSkillTrigger.ManualCombat,
                        "battle:" + data.battleId + ":command:" + data.commandId + ":offense-ultimate",
                        OwnedSession, actingCombatant, target), actor.Progression.Ultimate,
                    "battle:" + data.battleId + ":command:" + data.commandId + ":offense-ultimate",
                    CharacterGrowthRules.StableNonNegativeHash(data.battleId), commandPublications.ReservedPublicationKey);
                if (!skillActivationTransactions.TryBeginAppliedAutomatic(request, out scope, out string beginFailure))
                { result = new OffenseBattleCommandResult(false, beginFailure); return false; }
            }
            if (!execute(command, out result)) return false;
            PreparedCharacterSkillAppliedActivation skill = default;
            if (ultimate && !skillActivationTransactions.TryPrepareAppliedAutomatic(scope, out skill, out string prepareFailure))
            { result = new OffenseBattleCommandResult(false, prepareFailure); return false; }
            data.resolved = true;
            data.weaponInstanceId = actingCombatant.Weapon?.InstanceId ?? string.Empty;
            data.weaponDefinitionId = actingCombatant.Weapon?.DefinitionId ?? string.Empty;
            data.targetHealthAfter = OwnedSession.FindCombatant(command.TargetId)?.CurrentHealth ?? data.targetHealthBefore;
            data.targetIsDead = OwnedSession.FindCombatant(command.TargetId)?.IsDead ?? data.targetWasDead;
            data.amount = result.Amount; data.hit = result.Hit;
            data.shieldBlocked = result.ShieldBlocked; data.coverBlocked = result.CoverBlocked;
            var delivery = commandPublications.PublishApplied(new OffenseBattleCommandFact(data), skill.Fact, skill.Lease);
            effectsRetained = delivery.MayBeCommitted;
            if (!effectsRetained)
            { result = new OffenseBattleCommandResult(false, delivery.Detail); return false; }
        }
        finally
        {
            // A saved pending row is authoritative even when the publisher throws.
            // Only an explicit rejection can undo the already executed command.
            effectsRetained |= commandPublications.HasPending(data.battleId);
            if (!effectsRetained)
            {
                skillActivationTransactions?.RollbackAppliedAutomatic(scope);
                RestoreRejectedBattleCommand(before);
            }
            commandPublications.End();
        }
        if (!RecoverCommandObservation())
            result = new OffenseBattleCommandResult(true,
                result.Message + " (기록/후속 처리 대기: " + commandPublications.LastFailure + ")",
                result.Amount, result.Hit, result.ShieldBlocked, result.CoverBlocked);
        return true;
    }

    private bool RecoverCommandObservation()
    {
        if (restoring || recoveringCommand) return false;
        if (commandPublications == null || !commandPublications.HasAnyPending) return true;
        if (OwnedSession == null) return false;
        recoveringCommand = true;
        try
        {
            if (!commandPublications.RecoverPending(out _)) return false;
            var row = commandPublications.PendingForBattle(OwnedSession.BattleId);
            if (row == null) throw new InvalidOperationException("battle-command-pending-session-missing");
            var fact = row.fact;
            if (OwnedSession.LastProcessedCommandId != fact.commandId)
                throw new InvalidOperationException("battle-command-pending-revision-conflict");
            var actor = OwnedSession.FindCombatant(fact.actorId);
            var target = OwnedSession.FindCombatant(fact.targetId);
            var command = new OffenseBattleCommand(fact.commandId, fact.actorId, fact.actionType, fact.targetId, fact.abilityId);
            var result = new OffenseBattleCommandResult(true, string.Empty, fact.amount, fact.hit, fact.shieldBlocked, fact.coverBlocked);
            while (row.followupStage < OffenseBattleCommandPublicationRuntime.FollowupsComplete)
            {
                switch (row.followupStage)
                {
                    case 0:
                        if (row.hasSkill && (!TryGetActor(fact.actorId, out var source)
                            || !skillActivationTransactions.TryFinalizeAppliedAutomatic(source,
                                new CharacterSkillActivationFact(row.skill),
                                OffenseBattleCommandPublicationRuntime.SkillDelivery(row), out _)))
                            throw new InvalidOperationException("battle-command-skill-mirror-pending");
                        break;
                    case 1:
                        AwardCombatCommand(command, actor, result, target, fact.targetHealthBefore, fact.targetWasDead, fact.absoluteHour);
                        break;
                    case 2:
                        AwardDefensiveCombat(command, actor, target, result, fact.absoluteHour);
                        break;
                    case 3:
                        if (target?.Team == OffenseBattleTeam.Allies && fact.targetHealthAfter < fact.targetHealthBefore
                            && TryGetActor(target.PersistentId, out var damaged))
                            CharacterSkillRuntimeEffects.ApplyTriggeredPassives(new CharacterSkillExecutionContext(
                                damaged, CharacterSkillTrigger.DamageTaken, $"{fact.battleId}:{fact.commandId}:damage-taken",
                                OwnedSession, target, actor));
                        break;
                    case 4:
                        if (actor?.Team == OffenseBattleTeam.Allies && target?.Team == OffenseBattleTeam.Enemies
                            && !fact.targetWasDead && fact.targetIsDead && TryGetActor(actor.PersistentId, out var attacker))
                            CharacterSkillRuntimeEffects.ApplyTriggeredPassives(new CharacterSkillExecutionContext(
                                attacker, CharacterSkillTrigger.EnemyDefeated, $"{fact.battleId}:{fact.commandId}:enemy-defeated",
                                OwnedSession, actor, target));
                        break;
                }
                commandPublications.AdvanceFollowup(fact.battleId, fact.commandId, row.followupStage++);
            }
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        { commandPublications.RecordFollowupFailure(exception); return false; }
        finally { recoveringCommand = false; }
    }

    private void RestoreRejectedBattleCommand(
        OffenseBattlePersistenceState commandBefore)
    {
        if (commandBefore == null || OwnedSession == null)
            return;
        OffenseExpeditionRun settlementOwner = OwnedSession.SettlementOwner;
        OwnedSession = OffenseBattleSession.Restore(
            commandBefore,
            OwnedSession.Combatants,
            combatResolution,
            combatEquipmentRuntime,
            OwnedSession.EncounterRules);
        if (settlementOwner != null)
            OwnedSession.BindSettlementOwner(settlementOwner);
    }


    private void AwardCombatCommand(
        OffenseBattleCommand command,
        OffenseBattleCombatant actingCombatant,
        OffenseBattleCommandResult commandResult,
        OffenseBattleCombatant targetAfter,
        float targetHealthBefore,
        bool targetWasDead,
        long absoluteHour)
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
            stableAwardKey: $"{OwnedSession.BattleId}:{command.CommandId}:attack",
            absoluteHour: absoluteHour);
    }

    private void AwardDefensiveCombat(
        OffenseBattleCommand command,
        OffenseBattleCombatant actingCombatant,
        OffenseBattleCombatant targetAfter,
        OffenseBattleCommandResult commandResult,
        long absoluteHour)
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
            stableAwardKey: $"{OwnedSession.BattleId}:{command.CommandId}:block:{targetAfter.PersistentId}",
            absoluteHour: absoluteHour);
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
        combatant.SetAmmunitionProfile(CombatResolutionService.ResolveAmmunitionProfile(
            itemCatalog, weapon?.AmmunitionItemId));
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
        combatant.SetArcanePowerMultiplier(arcane.Value
            * actor.Stats.GetDetailedStatMultiplier(GameplayEffectTargetIds.ArcanePower));
    }

    private void SynchronizeAlliedBodyHealth()
    {
        if (OwnedSession == null || bodyHealthCommands == null)
        {
            return;
        }

        foreach (OffenseBattleCombatant combatant in OwnedSession.Combatants
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
