using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
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
        string actorId,
        string targetId,
        string abilityId,
        out OffenseBattleCommandResult result);
    bool FinalizePlannedTurn();
    bool TryGetActor(string persistentId, out CharacterActor actor);
    OffenseBattlePersistenceState CapturePersistentState();
    bool TryRestoreBattle(
        OffenseExpeditionRun expedition,
        OffenseBattlePersistenceState state,
        out string message);
    void ClearForPersistentRestore();
    void SetBattleViewVisible(bool visible);
    void ClearCompletedBattle();
}

public sealed class OffenseBattleRuntime :
    IOffenseBattleRuntime,
    IStartable,
    IDisposable
{
    private readonly ICharacterWorldSaveService characterSaveService;
    private readonly IRunVariableRuntimeProvider runVariableRuntimeProvider;
    private readonly ICombatResolutionService combatResolution;
    private readonly ICombatEquipmentRuntime combatEquipmentRuntime;
    private readonly ICharacterBodyHealthRuntime bodyHealthRuntime;
    private readonly IOffenseRegionRuntime offenseRegionRuntime;
    private readonly IGameEventBus gameEventBus;
    private readonly Dictionary<string, CharacterActor> actorsById =
        new Dictionary<string, CharacterActor>(StringComparer.Ordinal);
    private bool started;
    private bool completionRaised;
    private IDisposable ownerRunEndedSubscription;

    public OffenseBattleRuntime(
        ICharacterWorldSaveService characterSaveService,
        IRunVariableRuntimeProvider runVariableRuntimeProvider,
        IGameEventBus gameEventBus,
        ICombatResolutionService combatResolution = null,
        ICombatEquipmentRuntime combatEquipmentRuntime = null,
        ICharacterBodyHealthRuntime bodyHealthRuntime = null,
        IOffenseRegionRuntime offenseRegionRuntime = null)
    {
        this.characterSaveService = characterSaveService
            ?? throw new ArgumentNullException(nameof(characterSaveService));
        this.runVariableRuntimeProvider = runVariableRuntimeProvider
            ?? throw new ArgumentNullException(nameof(runVariableRuntimeProvider));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.combatResolution = combatResolution;
        this.combatEquipmentRuntime = combatEquipmentRuntime;
        this.bodyHealthRuntime = bodyHealthRuntime;
        this.offenseRegionRuntime = offenseRegionRuntime;
    }

    public OffenseBattleSession Session { get; private set; }
    public bool HasActiveBattle => Session != null && !Session.IsComplete;
    public bool IsBattleViewVisible { get; private set; }
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
        combatants.AddRange(OffenseEncounterCatalog.CreateEnemies(
            expedition.Target,
            difficulty,
            expedition.Phase == OffenseExpeditionPhase.InBattle ? expedition.CurrentNode : null,
            offenseRegionRuntime?.GetPressureForTarget(expedition.Target) ?? default));
        Session = new OffenseBattleSession(
            Guid.NewGuid().ToString("N"),
            expedition.ExpeditionId,
            expedition.Target.id,
            expedition.Target.title,
            difficulty,
            combatants,
            combatResolution,
            combatEquipmentRuntime);
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
        string actorId,
        string targetId,
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

        OffenseBattleActionType actionType = string.IsNullOrWhiteSpace(abilityId)
            || string.Equals(abilityId, "basic", StringComparison.Ordinal)
                ? OffenseBattleActionType.BasicAttack
                : OffenseBattleActionType.Ability;
        OffenseBattleCommand command = new OffenseBattleCommand(
            Session.LastProcessedCommandId + 1,
            actorId,
            actionType,
            targetId,
            actionType == OffenseBattleActionType.Ability ? abilityId : string.Empty);
        bool accepted = Session.TryExecutePlannedCommand(command, out result);
        if (!accepted)
        {
            return false;
        }

        StateChanged?.Invoke();
        return true;
    }

    public bool FinalizePlannedTurn()
    {
        if (Session == null)
        {
            return false;
        }

        StateChanged?.Invoke();
        return RaiseCompletionIfNeeded();
    }

    public bool TryGetActor(string persistentId, out CharacterActor actor)
    {
        actor = null;
        return !string.IsNullOrWhiteSpace(persistentId)
            && actorsById.TryGetValue(persistentId, out actor)
            && actor != null;
    }

    public OffenseBattlePersistenceState CapturePersistentState()
    {
        return HasActiveBattle ? Session.CapturePersistentState() : null;
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
                member.Formation,
                member.Stress);
            ConfigureCombatEquipment(combatant);
            ConfigureBodyHealth(actor, combatant);
            combatants.Add(combatant);
        }

        combatants.AddRange(OffenseEncounterCatalog.CreateEnemies(
            expedition.Target,
            state.difficulty,
            expedition.Phase == OffenseExpeditionPhase.InBattle ? expedition.CurrentNode : null,
            offenseRegionRuntime?.GetPressureForTarget(expedition.Target) ?? default));
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
            combatEquipmentRuntime);
        completionRaised = false;
        IsBattleViewVisible = true;
        StateChanged?.Invoke();
        if (!RaiseCompletionIfNeeded()) RunEnemyTurns();
        message = "전투를 현재 턴에서 복원했습니다.";
        return true;
    }

    public void ClearForPersistentRestore()
    {
        Session = null;
        actorsById.Clear();
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
            OffenseBattleCommand command = Session.CreateEnemyCommand(Session.LastProcessedCommandId + 1);
            if (command == null || !TryExecuteSessionCommand(command, out _)) break;
            StateChanged?.Invoke();
        }

        RaiseCompletionIfNeeded();
    }

    private bool RaiseCompletionIfNeeded()
    {
        if (Session == null || !Session.IsComplete || completionRaised) return false;
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
            combatEquipmentRuntime.GetArmor(combatant.PersistentId));
    }

    private void ConfigureBodyHealth(CharacterActor actor, OffenseBattleCombatant combatant)
    {
        if (actor == null || combatant == null || bodyHealthRuntime == null)
        {
            return;
        }

        combatant.ApplyBodyHealth(bodyHealthRuntime.GetSnapshot(actor));
    }

    private void SynchronizeAlliedBodyHealth()
    {
        if (Session == null || bodyHealthRuntime == null)
        {
            return;
        }

        foreach (OffenseBattleCombatant combatant in Session.Combatants
            .Where(value => value.Team == OffenseBattleTeam.Allies))
        {
            if (TryGetActor(combatant.PersistentId, out CharacterActor actor))
            {
                bodyHealthRuntime.ApplySnapshot(
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
        if (runVariableRuntimeProvider.TryGetRuntime(out RunVariableRuntime runtime)
            && runtime.State.StartVariables != null)
        {
            return runtime.State.StartVariables.runDifficulty;
        }

        return DungeonDifficulty.Normal;
    }
}
