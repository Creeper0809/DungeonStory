using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using VContainer;

public class OffenseExpeditionRuntime : MonoBehaviour
{
    private const int MaxResultHistory = 20;

    private readonly List<OffenseExpeditionRun> activeExpeditions = new List<OffenseExpeditionRun>();
    private readonly List<OffenseExpeditionResult> resultHistory = new List<OffenseExpeditionResult>();
    private IReadOnlyList<OffenseExpeditionRun> activeExpeditionsView;
    private IReadOnlyList<OffenseExpeditionResult> resultHistoryView;
    private IOffenseExpeditionMemberQuery memberQuery;
    private IOffenseWorldMapRuntimeProvider worldMapProvider;
    private IOffenseRewardRuntimeProvider rewardProvider;
    private IMetaProgressionRuntimeProvider metaProgressionProvider;
    private IOffensePanelService panelService;
    private IOffenseBattleRuntime battleRuntime;
    private IOffensePreparationService preparationService;
    private IExpeditionEquipmentRuntime equipmentRuntime;
    private IExpeditionDepartureService departureService;
    private IExpeditionReturnService returnService;
    private IGameEventBus gameEventBus;

    public IReadOnlyList<OffenseExpeditionRun> ActiveExpeditions =>
        activeExpeditionsView ??= activeExpeditions.AsReadOnly();
    public IReadOnlyList<OffenseExpeditionResult> ResultHistory =>
        resultHistoryView ??= resultHistory.AsReadOnly();
    public event Action StateChanged;

    public void Construct(
        IOffenseExpeditionMemberQuery memberQuery,
        IOffenseWorldMapRuntimeProvider worldMapProvider,
        IOffenseRewardRuntimeProvider rewardProvider,
        IMetaProgressionRuntimeProvider metaProgressionProvider,
        IOffensePanelService panelService,
        IGameEventBus gameEventBus)
    {
        this.memberQuery = memberQuery
            ?? throw new ArgumentNullException(nameof(memberQuery));
        this.worldMapProvider = worldMapProvider
            ?? throw new ArgumentNullException(nameof(worldMapProvider));
        this.rewardProvider = rewardProvider
            ?? throw new ArgumentNullException(nameof(rewardProvider));
        this.metaProgressionProvider = metaProgressionProvider
            ?? throw new ArgumentNullException(nameof(metaProgressionProvider));
        this.panelService = panelService
            ?? throw new ArgumentNullException(nameof(panelService));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public void Construct(
        IOffenseExpeditionMemberQuery memberQuery,
        IOffenseWorldMapRuntimeProvider worldMapProvider,
        IOffenseRewardRuntimeProvider rewardProvider,
        IMetaProgressionRuntimeProvider metaProgressionProvider,
        IOffensePanelService panelService,
        IGameEventBus gameEventBus,
        IOffenseBattleRuntime battleRuntime)
    {
        Construct(
            memberQuery,
            worldMapProvider,
            rewardProvider,
            metaProgressionProvider,
            panelService,
            gameEventBus);
        if (this.battleRuntime != null)
        {
            this.battleRuntime.BattleCompleted -= OnBattleCompleted;
        }

        this.battleRuntime = battleRuntime
            ?? throw new ArgumentNullException(nameof(battleRuntime));
        this.battleRuntime.BattleCompleted += OnBattleCompleted;
    }

    [Inject]
    public void Construct(
        IOffenseExpeditionMemberQuery memberQuery,
        IOffenseWorldMapRuntimeProvider worldMapProvider,
        IOffenseRewardRuntimeProvider rewardProvider,
        IMetaProgressionRuntimeProvider metaProgressionProvider,
        IOffensePanelService panelService,
        IOffenseBattleRuntime battleRuntime,
        IOffensePreparationService preparationService,
        IExpeditionEquipmentRuntime equipmentRuntime,
        IGameEventBus gameEventBus,
        IExpeditionDepartureService departureService = null,
        IExpeditionReturnService returnService = null)
    {
        Construct(
            memberQuery,
            worldMapProvider,
            rewardProvider,
            metaProgressionProvider,
            panelService,
            gameEventBus,
            battleRuntime);
        this.preparationService = preparationService
            ?? throw new ArgumentNullException(nameof(preparationService));
        this.equipmentRuntime = equipmentRuntime
            ?? throw new ArgumentNullException(nameof(equipmentRuntime));
        this.departureService = departureService;
        this.returnService = returnService;
    }

    private void OnDestroy()
    {
        if (battleRuntime != null)
        {
            battleRuntime.BattleCompleted -= OnBattleCompleted;
        }
    }

    public IReadOnlyList<CharacterActor> GetAvailableMemberActors()
    {
        return ResolveMemberQuery().GetAvailableMemberActors();
    }

    public OffensePreparationSnapshot GetPreparationSnapshot()
    {
        return preparationService?.Evaluate()
            ?? new OffensePreparationSnapshot(
                new OffenseExpeditionPreparation(),
                new Dictionary<OffenseSupplyType, int>());
    }

    public IReadOnlyList<ExpeditionEquipmentDefinition> GetEquipmentDefinitions()
    {
        return equipmentRuntime?.Definitions ?? Array.Empty<ExpeditionEquipmentDefinition>();
    }

    public IReadOnlyDictionary<string, int> GetEquipmentInventory()
    {
        return equipmentRuntime?.Inventory
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
    }

    public IReadOnlyList<ExpeditionEquipmentCraftOrderSaveData> GetEquipmentCraftQueue()
    {
        return equipmentRuntime?.CraftQueue ?? Array.Empty<ExpeditionEquipmentCraftOrderSaveData>();
    }

    public int GetAvailableEquipmentCount(string equipmentId)
    {
        return equipmentRuntime?.GetAvailableCount(equipmentId) ?? 0;
    }

    public ExpeditionEquipmentStatBlock GetEquipmentBonuses(CharacterActor actor)
    {
        return equipmentRuntime?.GetCombatBonuses(GetPersistentCharacterId(actor))
            ?? ExpeditionEquipmentStatBlock.Empty;
    }

    public bool TryGetEquippedEquipment(
        CharacterActor actor,
        ExpeditionEquipmentSlot slot,
        out ExpeditionEquipmentDefinition definition)
    {
        definition = null;
        string characterId = GetPersistentCharacterId(actor);
        return !string.IsNullOrWhiteSpace(characterId)
            && equipmentRuntime != null
            && equipmentRuntime.TryGetEquipped(characterId, slot, out string equipmentId)
            && equipmentRuntime.TryGetDefinition(equipmentId, out definition);
    }

    public bool TryEquipEquipment(CharacterActor actor, string equipmentId, out string message)
    {
        string characterId = GetPersistentCharacterId(actor);
        if (string.IsNullOrWhiteSpace(characterId) || equipmentRuntime == null)
        {
            message = "equipment-runtime-missing";
            return false;
        }

        bool equipped = equipmentRuntime.TryEquip(characterId, equipmentId, out message);
        if (equipped)
        {
            StateChanged?.Invoke();
        }

        return equipped;
    }

    public bool TryUnequipEquipment(
        CharacterActor actor,
        ExpeditionEquipmentSlot slot,
        out string message)
    {
        string characterId = GetPersistentCharacterId(actor);
        if (string.IsNullOrWhiteSpace(characterId) || equipmentRuntime == null)
        {
            message = "equipment-runtime-missing";
            return false;
        }

        equipmentRuntime.Unequip(characterId, slot);
        message = "equipment-unequipped";
        StateChanged?.Invoke();
        return true;
    }

    public bool TryQueueEquipmentCraft(string equipmentId, out string message)
    {
        if (equipmentRuntime == null)
        {
            message = "equipment-runtime-missing";
            return false;
        }

        bool queued = equipmentRuntime.TryQueueCraft(equipmentId, out message);
        if (queued)
        {
            StateChanged?.Invoke();
        }

        return queued;
    }

    public bool TryStartExpedition(
        string targetId,
        IEnumerable<CharacterActor> members,
        out OffenseExpeditionRun expedition,
        out string message)
    {
        return TryStartExpedition(
            targetId,
            members,
            new OffenseSupplyLoadout(),
            new OffenseExpeditionPreparation(),
            out expedition,
            out message);
    }

    public bool TryStartExpedition(
        string targetId,
        IEnumerable<CharacterActor> members,
        OffenseSupplyLoadout supplies,
        OffenseExpeditionPreparation preparation,
        out OffenseExpeditionRun expedition,
        out string message)
    {
        expedition = null;
        if (!ResolveWorldMapProvider().TryGetRuntime(out OffenseWorldMapRuntime worldMap))
        {
            message = "월드맵이 초기화되지 않았습니다";
            return false;
        }

        OffenseTargetDefinition target = OffenseWorldMapService.FindKnownTarget(
            worldMap.State,
            worldMap.TargetDefinitions,
            targetId);
        if (target == null)
        {
            message = "발견되지 않은 원정 대상입니다";
            return false;
        }

        if (!OffenseWorldMapService.CanAttemptTarget(worldMap.State, target, out message))
        {
            return false;
        }

        if (activeExpeditions.Any(active => active?.Target != null
            && string.Equals(active.Target.id, target.id, StringComparison.Ordinal)))
        {
            message = "이미 해당 목표로 원정대가 출발했습니다.";
            return false;
        }

        if (activeExpeditions.Count > 0)
        {
            message = "한 번에 하나의 원정대만 지휘할 수 있습니다.";
            return false;
        }

        if (battleRuntime == null)
        {
            message = "전투 런타임이 준비되지 않았습니다.";
            return false;
        }

        List<CharacterActor> party = OffenseExpeditionService
            .GetDistinctMembers(members)
            .ToList();
        if (party.Count < target.requiredMembers)
        {
            message = $"필요 인력 부족: {party.Count}/{target.requiredMembers}";
            return false;
        }

        if (party.Count > 3)
        {
            message = $"원정대는 최대 3명까지 참가할 수 있습니다. ({party.Count}/3)";
            return false;
        }

        foreach (CharacterActor member in party)
        {
            if (!OffenseExpeditionService.CanJoinExpedition(member, out string reason))
            {
                message = $"{member.name}: {reason}";
                return false;
            }
        }

        supplies ??= new OffenseSupplyLoadout();
        preparation ??= new OffenseExpeditionPreparation();
        string expeditionId = Guid.NewGuid().ToString("N");
        if (supplies.TotalCount > 0)
        {
            if (preparationService == null)
            {
                message = "던전 보급 시스템이 준비되지 않았습니다.";
                expedition = null;
                return false;
            }

            if (!preparationService.TryCommitLoadout(supplies, preparation, expeditionId, out message))
            {
                expedition = null;
                return false;
            }
        }

        float totalPower = OffenseExpeditionService.CalculatePartyPower(party);
        expedition = new OffenseExpeditionRun(
            expeditionId,
            target,
            party,
            totalPower,
            target.durationSeconds,
            OffenseRouteGenerator.Create(target),
            supplies,
            preparation);
        if (departureService != null)
        {
            if (!departureService.TryBeginDeparture(
                    expedition,
                    party,
                    () => StateChanged?.Invoke(),
                    out string departureMessage))
            {
                preparationService?.ReturnSupplies(supplies, expeditionId);
                message = departureMessage;
                expedition = null;
                return false;
            }

            preparationService?.ConsumePackedSupplies(expeditionId);
            activeExpeditions.Add(expedition);
            message = $"{target.title} 출정 집결 중";
            gameEventBus.RaiseAlert("출정 집결", message, EventAlertImportance.Medium, "expedition");
            StateChanged?.Invoke();
            return true;
        }

        List<CharacterActor> departedMembers = new List<CharacterActor>();
        foreach (CharacterActor member in party)
        {
            if (!member.BeginExpedition())
            {
                foreach (CharacterActor departed in departedMembers)
                {
                    departed.EndExpedition(alive: true);
                }

                preparationService?.ReturnSupplies(supplies, expeditionId);
                message = $"{member.name}: 원정 상태로 전환할 수 없습니다.";
                expedition = null;
                return false;
            }

            departedMembers.Add(member);
        }

        preparationService?.ConsumePackedSupplies(expeditionId);
        activeExpeditions.Add(expedition);
        message = $"{target.title} 원정 출발: 경로를 선택하세요.";
        gameEventBus.RaiseAlert("원정 출발", message, EventAlertImportance.Medium, "오펜스");
        StateChanged?.Invoke();
        return true;
    }

    public bool TryChooseRouteNode(string expeditionId, string nodeId, out string message)
    {
        OffenseExpeditionRun expedition = FindActiveExpedition(expeditionId);
        if (expedition == null)
        {
            message = "진행 중인 원정을 찾을 수 없습니다.";
            return false;
        }

        if (!expedition.TryEnterNode(nodeId, out message))
        {
            return false;
        }

        if (expedition.Phase == OffenseExpeditionPhase.InBattle)
        {
            if (!battleRuntime.TryStartBattle(expedition, out message))
            {
                expedition.Retreat(out _);
                CompleteExpedition(expedition, success: false, "전투를 시작하지 못해 철수했습니다.");
                return false;
            }

            battleRuntime.AdvanceToPlayerDecision();
        }

        StateChanged?.Invoke();
        return true;
    }

    public bool TryResolveCurrentNode(
        string expeditionId,
        bool useSupply,
        out OffenseExpeditionNodeResult result,
        out string message)
    {
        OffenseExpeditionRun expedition = FindActiveExpedition(expeditionId);
        if (expedition == null)
        {
            result = null;
            message = "진행 중인 원정을 찾을 수 없습니다.";
            return false;
        }

        OffenseRouteNode resolvedNode = expedition.CurrentNode;
        bool resolved = expedition.TryResolveCurrentNode(useSupply, out result, out message);
        if (resolved)
        {
            AwardNodeExperience(expedition, resolvedNode);
            StateChanged?.Invoke();
        }

        return resolved;
    }

    public bool TryUseSupply(
        string expeditionId,
        OffenseSupplyType type,
        int memberIndex,
        out string message)
    {
        OffenseExpeditionRun expedition = FindActiveExpedition(expeditionId);
        if (expedition == null)
        {
            message = "진행 중인 원정을 찾을 수 없습니다.";
            return false;
        }

        bool used = expedition.TryUseSupply(type, memberIndex, out message);
        if (used) StateChanged?.Invoke();
        return used;
    }

    public bool TrySwapFormation(
        string expeditionId,
        int firstIndex,
        int secondIndex,
        out string message)
    {
        OffenseExpeditionRun expedition = FindActiveExpedition(expeditionId);
        if (expedition == null)
        {
            message = "진행 중인 원정을 찾을 수 없습니다.";
            return false;
        }

        bool swapped = expedition.TrySwapFormation(firstIndex, secondIndex, out message);
        if (swapped) StateChanged?.Invoke();
        return swapped;
    }

    public bool TryRetreat(string expeditionId, out string message)
    {
        OffenseExpeditionRun expedition = FindActiveExpedition(expeditionId);
        if (expedition == null)
        {
            message = "진행 중인 원정을 찾을 수 없습니다.";
            return false;
        }

        if (expedition.Phase == OffenseExpeditionPhase.InBattle)
        {
            message = "전투 중에는 행동 메뉴의 후퇴 명령을 사용해야 합니다.";
            return false;
        }

        if (!expedition.Retreat(out message))
        {
            return false;
        }

        CompleteExpedition(expedition, success: false, message);
        return true;
    }

    public void RestorePersistentState(
        IEnumerable<OffenseExpeditionRun> restoredActiveExpeditions,
        IEnumerable<OffenseExpeditionResult> restoredResultHistory)
    {
        activeExpeditions.Clear();
        activeExpeditions.AddRange((restoredActiveExpeditions ?? Array.Empty<OffenseExpeditionRun>())
            .Where(expedition => expedition != null && expedition.Target != null));
        resultHistory.Clear();
        resultHistory.AddRange((restoredResultHistory ?? Array.Empty<OffenseExpeditionResult>())
            .Where(result => result != null)
            .Take(MaxResultHistory));
        StateChanged?.Invoke();
    }

    private void OnBattleCompleted(OffenseBattleSession session)
    {
        if (session == null)
        {
            return;
        }

        int index = activeExpeditions.FindIndex(expedition => string.Equals(
            expedition?.ExpeditionId,
            session.ExpeditionId,
            StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        OffenseExpeditionRun expedition = activeExpeditions[index];
        OffenseRouteNode completedNode = expedition.CurrentNode;
        bool victory = session.Outcome == OffenseBattleOutcome.Victory;
        foreach (OffenseBattleCombatant combatant in session.Combatants
            .Where(value => value.Team == OffenseBattleTeam.Allies))
        {
            if (!battleRuntime.TryGetActor(combatant.PersistentId, out CharacterActor actor))
            {
                continue;
            }

            float healthDelta = combatant.CurrentHealth - actor.CurrentHealth;
            if (healthDelta < 0f)
            {
                actor.ApplyDamage(-healthDelta, "원정 전투 피해");
            }
            else if (healthDelta > 0f)
            {
                actor.Heal(healthDelta);
            }

            bool survived = !combatant.IsDead && !actor.IsDead;
            OffenseExpeditionMemberState member = expedition.MemberStates
                .FirstOrDefault(value => value.Actor == actor);
            if (member != null)
            {
                member.Formation = combatant.Formation;
            }
            expedition.RecordBattleMemberResult(actor, combatant.TotalDamageTaken, survived);
            if (survived)
            {
                int battleExperience = victory
                    ? CalculateNodeExperience(completedNode, Mathf.Clamp(expedition.Target.campaignOrder, 1, 6))
                    : 10;
                actor.Progression?.AddExperience(battleExperience);
                CharacterSkillRuntimeEffects.ApplyTriggeredPassives(new CharacterSkillExecutionContext(
                    actor,
                    CharacterSkillTrigger.BattleCompleted,
                    $"{session.BattleId}:battle-completed:{combatant.PersistentId}",
                    session,
                    combatant,
                    null));
                actor.Progression?.RecordNarrative(
                    CharacterNarrativeDomain.Combat,
                    victory ? "battle-victory" : "battle-survived",
                    expedition.Target.id,
                    victory ? "won" : "survived",
                    combatant.TotalDamageTaken,
                    triggerPassives: false);
            }
            actor.Stats?.ChangesStat(CharacterCondition.SLEEP, victory ? -8f : -20f);
            actor.Stats?.ApplyMoodFactor(
                victory ? "offense:battle-victory" : "offense:battle-failure",
                victory ? "원정 교전을 이겨냄" : "원정 전투에서 무너짐",
                victory ? 2f : -10f,
                240f,
                1);
        }

        if (session.Outcome == OffenseBattleOutcome.Retreated)
        {
            expedition.Retreat(out string retreatMessage);
            battleRuntime.ClearCompletedBattle();
            CompleteExpedition(expedition, success: false, retreatMessage);
            return;
        }

        expedition.CompleteBattleNode(victory);
        battleRuntime.ClearCompletedBattle();
        if (!expedition.IsComplete)
        {
            StateChanged?.Invoke();
            gameEventBus.RaiseAlert(
                "원정 교전 승리",
                $"{expedition.CurrentNode?.Title ?? expedition.Target.title}을 돌파했습니다. 다음 경로를 선택하세요.",
                EventAlertImportance.Low,
                "오펜스");
            return;
        }

        CompleteExpedition(
            expedition,
            victory && expedition.Phase == OffenseExpeditionPhase.Completed,
            victory ? "목표를 쓰러뜨리고 귀환했습니다." : "원정대가 전투에서 패배했습니다.");
    }

    private OffenseExpeditionRun FindActiveExpedition(string expeditionId)
    {
        return activeExpeditions.FirstOrDefault(expedition => string.Equals(
            expedition?.ExpeditionId,
            expeditionId,
            StringComparison.Ordinal));
    }

    private static void AwardNodeExperience(OffenseExpeditionRun expedition, OffenseRouteNode node)
    {
        if (expedition == null || node == null)
        {
            return;
        }

        int stage = Mathf.Clamp(expedition.Target?.campaignOrder ?? 1, 1, 6);
        int experience = CalculateNodeExperience(node, stage);
        if (experience <= 0)
        {
            return;
        }

        foreach (OffenseExpeditionMemberState member in expedition.MemberStates)
        {
            CharacterActor actor = member?.Actor;
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            actor.Progression?.AddExperience(experience);
            actor.Progression?.RecordNarrative(
                CharacterNarrativeDomain.Expedition,
                $"node:{node.Kind}",
                expedition.Target?.id ?? string.Empty,
                "resolved",
                experience);
        }
    }

    public static int CalculateNodeExperience(OffenseRouteNode node, int stage)
    {
        int normalizedStage = Mathf.Clamp(stage, 1, 6);
        return node?.Kind switch
        {
            OffenseRouteNodeKind.Event => 35 + normalizedStage * 10,
            OffenseRouteNodeKind.Camp => 35 + normalizedStage * 10,
            OffenseRouteNodeKind.Cache => 35 + normalizedStage * 10,
            OffenseRouteNodeKind.Battle => IsEliteBattleNode(node)
                ? 100 + normalizedStage * 25
                : 80 + normalizedStage * 20,
            OffenseRouteNodeKind.Boss => 140 + normalizedStage * 30,
            _ => 0
        };
    }

    private static bool IsEliteBattleNode(OffenseRouteNode node)
    {
        return node != null
            && node.Kind == OffenseRouteNodeKind.Battle
            && (node.DangerMultiplier >= 0.95f
                || (!string.IsNullOrWhiteSpace(node.Id)
                    && node.Id.IndexOf("elite", StringComparison.OrdinalIgnoreCase) >= 0));
    }

    public static int CalculateSuccessfulReturnExperience(OffenseExpeditionRun expedition)
    {
        return CalculateSuccessfulReturnExperience(expedition?.Target?.campaignOrder ?? 1);
    }

    public static int CalculateSuccessfulReturnExperience(int stage)
    {
        return 60 + Mathf.Clamp(stage, 1, 6) * 20;
    }

    private void CompleteExpedition(OffenseExpeditionRun expedition, bool success, string message)
    {
        if (expedition == null) return;

        activeExpeditions.Remove(expedition);
        preparationService?.ReturnSupplies(expedition.Supplies, expedition.ExpeditionId);
        preparationService?.DepositLoot(expedition.CarriedStock);
        List<OffenseExpeditionMemberSnapshot> members = new List<OffenseExpeditionMemberSnapshot>();
        foreach (OffenseExpeditionMemberState member in expedition.MemberStates)
        {
            CharacterActor actor = member.Actor;
            if (actor == null) continue;
            bool survived = !actor.IsDead;
            actor.Lifecycle?.RecordExpeditionReturn(member.Stress, survived);
            bool returnAnimated = survived
                && returnService != null
                && returnService.TryBeginReturn(
                    actor,
                    true,
                    () => StateChanged?.Invoke(),
                    out _);
            if (!returnAnimated)
            {
                actor.EndExpedition(survived);
            }

            if (success && survived)
            {
                actor.Progression?.AddExperience(CalculateSuccessfulReturnExperience(expedition));
            }
            else if (!survived)
            {
                actor.EnsureRuntimeState();
                equipmentRuntime?.HandleCharacterDeath(actor.Identity?.PersistentId);
            }
            members.Add(new OffenseExpeditionMemberSnapshot(
                GetActorName(actor),
                actor.Identity != null ? actor.Identity.SpeciesTag : string.Empty,
                OffenseExpeditionService.CalculateMemberPower(actor),
                survived,
                member.TotalDamageTaken));
        }

        OffenseExpeditionResult result = new OffenseExpeditionResult(
            expedition.ExpeditionId,
            expedition.Target.id,
            expedition.Target.title,
            success,
            expedition.TotalPower,
            expedition.Target.requiredPower,
            expedition.Target.danger,
            expedition.TotalDurationSeconds - expedition.RemainingSeconds,
            members,
            success
                ? expedition.Target.rewards?
                    .Where(reward => reward != null)
                    .Select(reward => reward.ToSummaryText())
                    .ToArray() ?? Array.Empty<string>()
                : Array.Empty<string>());
        FinalizeBattleExpedition(expedition, result);
        StateChanged?.Invoke();
        if (!string.IsNullOrWhiteSpace(message))
        {
            gameEventBus.RaiseAlert(
                success ? "원정 완수" : "원정 종료",
                message,
                success ? EventAlertImportance.Medium : EventAlertImportance.High,
                "오펜스");
        }
    }

    private static string GetActorName(CharacterActor actor)
    {
        actor?.EnsureRuntimeState();
        return actor != null && actor.Identity != null
            ? actor.Identity.DisplayName
            : actor != null ? actor.name : "대원";
    }

    private OffenseExpeditionResult FinalizeBattleExpedition(
        OffenseExpeditionRun expedition,
        OffenseExpeditionResult result)
    {
        if (expedition == null || result == null)
        {
            return null;
        }

        if (result.success
            && ResolveMetaProgressionProvider().TryGetRuntime(out MetaProgressionRuntime metaProgression))
        {
            metaProgression.RecordOffenseSuccess();
        }

        if (result.success
            && ResolveRewardProvider().TryGetRuntime(out OffenseRewardRuntime rewards))
        {
            IReadOnlyList<OffenseRewardGrantResult> grantedRewards =
                rewards.ApplyExpeditionRewards(expedition, result);
            result = result.WithGrantedRewards(grantedRewards);
            gameEventBus.Publish(new OffenseRewardGrantedEvent(result, result.grantedRewards));
        }

        resultHistory.Insert(0, result);
        if (resultHistory.Count > MaxResultHistory)
        {
            resultHistory.RemoveRange(MaxResultHistory, resultHistory.Count - MaxResultHistory);
        }

        if (result.success)
        {
            if (!ResolveWorldMapProvider().TryGetRuntime(out OffenseWorldMapRuntime worldMap))
            {
                Debug.LogWarning("Successful battle could not update the offense campaign because the world map is missing.");
            }
            else if (!worldMap.TryRecordSuccessfulExpedition(result.targetId, out _, out string campaignMessage))
            {
                Debug.LogWarning($"Successful battle did not advance the offense campaign: {campaignMessage}");
            }
        }

        gameEventBus.RaiseAlert(
            "원정 전투 결과",
            result.ToDetailText(),
            result.success ? EventAlertImportance.Medium : EventAlertImportance.High,
            "오펜스");
        return result;
    }

    public OffenseExpeditionPanel ShowExpeditionPanel()
    {
        return ResolvePanelService().ShowExpedition(this);
    }

    private static string GetPersistentCharacterId(CharacterActor actor)
    {
        actor?.EnsureRuntimeState();
        return actor?.Identity?.PersistentId ?? string.Empty;
    }

    private IOffenseExpeditionMemberQuery ResolveMemberQuery()
    {
        return memberQuery
            ?? throw new InvalidOperationException($"{nameof(OffenseExpeditionRuntime)} requires {nameof(IOffenseExpeditionMemberQuery)} injection.");
    }

    private IOffenseWorldMapRuntimeProvider ResolveWorldMapProvider()
    {
        return worldMapProvider
            ?? throw new InvalidOperationException($"{nameof(OffenseExpeditionRuntime)} requires {nameof(IOffenseWorldMapRuntimeProvider)} injection.");
    }

    private IOffenseRewardRuntimeProvider ResolveRewardProvider()
    {
        return rewardProvider
            ?? throw new InvalidOperationException($"{nameof(OffenseExpeditionRuntime)} requires {nameof(IOffenseRewardRuntimeProvider)} injection.");
    }

    private IMetaProgressionRuntimeProvider ResolveMetaProgressionProvider()
    {
        return metaProgressionProvider
            ?? throw new InvalidOperationException($"{nameof(OffenseExpeditionRuntime)} requires {nameof(IMetaProgressionRuntimeProvider)} injection.");
    }

    private IOffensePanelService ResolvePanelService()
    {
        return panelService
            ?? throw new InvalidOperationException($"{nameof(OffenseExpeditionRuntime)} requires {nameof(IOffensePanelService)} injection.");
    }
}

public class OffenseExpeditionPanel : MonoBehaviour
{
    private enum JourneyButtonStyle
    {
        Action,
        Route,
        Supply,
        Danger,
        Close
    }

    private OffenseExpeditionRuntime runtime;
    private TMP_Text headerText;
    private TMP_Text detailText;
    private RectTransform memberButtonRoot;
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();
    private readonly List<CharacterActor> selectedMembers = new List<CharacterActor>();
    private readonly Dictionary<OffenseSupplyType, int> selectedSupplies =
        Enum.GetValues(typeof(OffenseSupplyType))
            .Cast<OffenseSupplyType>()
            .ToDictionary(type => type, _ => 0);
    private OffenseWorldMapRuntime worldMap;
    private string statusMessage;
    private IOffensePanelButtonFactory buttonFactory;

    public void Bind(
        OffenseExpeditionRuntime source,
        OffenseWorldMapRuntime worldMap,
        IOffensePanelButtonFactory buttonFactory)
    {
        if (!ReferenceEquals(runtime, source))
        {
            if (runtime != null) runtime.StateChanged -= Render;
            runtime = source ?? throw new ArgumentNullException(nameof(source));
            runtime.StateChanged += Render;
        }
        this.worldMap = worldMap;
        this.buttonFactory = buttonFactory
            ?? throw new ArgumentNullException(nameof(buttonFactory));
        EnsureView();
        gameObject.SetActive(true);
        Render();
    }

    public void Render()
    {
        if (runtime == null)
        {
            return;
        }

        EnsureView();
        ClearButtons();
        if (runtime.ActiveExpeditions.Count > 0)
        {
            RenderJourney(runtime.ActiveExpeditions[0]);
            return;
        }

        string selectedTargetId = worldMap != null ? worldMap.State.SelectedTargetId : string.Empty;
        OffenseTargetSnapshot target = null;
        if (worldMap != null && !string.IsNullOrWhiteSpace(selectedTargetId))
        {
            worldMap.TryGetKnownTargetSnapshot(selectedTargetId, out target);
        }

        headerText.text = target != null
            ? $"전투 편성 / 대상: {target.title} / 필요 {target.requiredMembers}명 / 최대 3명"
            : "원정 편성 / 선택된 대상 없음";

        foreach (CharacterActor member in runtime.GetAvailableMemberActors())
        {
            CharacterActor captured = member;
            string label = BuildMemberLabel(captured);
            GameObject buttonObject = RequireButtonFactory().CreateButton(
                memberButtonRoot,
                selectedMembers.Contains(captured) ? $"[선택] {label}" : label,
                16f,
                () =>
                {
                    if (selectedMembers.Contains(captured))
                    {
                        selectedMembers.Remove(captured);
                    }
                    else if (selectedMembers.Count < 3)
                    {
                        selectedMembers.Add(captured);
                    }
                    else
                    {
                        statusMessage = "원정대는 최대 3명입니다.";
                    }

                    Render();
                });
            spawnedButtons.Add(buttonObject);
            buttonObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 38f;
            StyleJourneyButton(
                buttonObject,
                selectedMembers.Contains(captured) ? JourneyButtonStyle.Supply : JourneyButtonStyle.Action);
        }

        OffensePreparationSnapshot preparation = runtime.GetPreparationSnapshot();
        foreach (OffenseSupplyType type in Enum.GetValues(typeof(OffenseSupplyType)))
        {
            OffenseSupplyType capturedType = type;
            int selected = selectedSupplies[type];
            int available = preparation.GetAvailable(type);
            AddButton(
                $"{OffenseSupplyCatalog.GetDisplayName(type)} {selected}/{available}  +",
                () => IncrementSupply(capturedType, preparation),
                JourneyButtonStyle.Supply);
        }

        if (selectedSupplies.Values.Any(value => value > 0))
        {
            AddButton("보급 초기화", () => ResetSupplies(), JourneyButtonStyle.Action);
        }

        AddButton(
            "원정 출발",
            () =>
            {
                if (string.IsNullOrWhiteSpace(selectedTargetId))
                {
                    statusMessage = "선택된 원정 대상이 없습니다.";
                    Render();
                    return;
                }

                OffenseSupplyLoadout loadout = new OffenseSupplyLoadout(selectedSupplies);
                if (runtime.TryStartExpedition(
                    selectedTargetId,
                    selectedMembers,
                    loadout,
                    preparation.Preparation,
                    out _,
                    out string message))
                {
                    selectedMembers.Clear();
                    ResetSupplies(render: false);
                    statusMessage = message;
                }
                else
                {
                    statusMessage = message;
                }

                Render();
            }, JourneyButtonStyle.Route);
        AddButton("닫기", Hide, JourneyButtonStyle.Close);

        RenderEquipmentButtons();

        float selectedPower = target != null
            ? OffenseExpeditionService.CalculatePartyPower(selectedMembers)
            : 0f;
        string detail = target != null
            ? $"{target.ToDetailText()}\n\n선택 인원: {selectedMembers.Count}/3"
                + $"\n원정대 전투력: {selectedPower:0.#} / 권장 {target.requiredPower:0.#}"
                + $"\n보급: {selectedSupplies.Values.Sum()}/{preparation.Preparation.SupplyCapacity}"
                + $"\n시작 조명 {preparation.Preparation.StartingLight:0}"
                + $" · 정찰 {preparation.Preparation.Scouting}"
                + $"\n야영 회복 {preparation.Preparation.CampHealRatio * 100f:0}%"
                + $" · 스트레스 -{preparation.Preparation.CampStressRecovery:0}"
                + BuildPreparationSources(preparation.Preparation)
            : $"선택 인원: {selectedMembers.Count}/3";
        string finalDetail = string.IsNullOrWhiteSpace(statusMessage)
            ? detail
            : $"{detail}\n\n{statusMessage}";
        detailText.text = finalDetail + BuildEquipmentDetail(target);
    }

    private void RenderEquipmentButtons()
    {
        if (runtime == null || selectedMembers.Count != 1)
        {
            return;
        }

        CharacterActor member = selectedMembers[0];
        foreach (ExpeditionEquipmentSlot slot in Enum.GetValues(typeof(ExpeditionEquipmentSlot)))
        {
            ExpeditionEquipmentSlot capturedSlot = slot;
            if (!runtime.TryGetEquippedEquipment(member, slot, out _))
            {
                continue;
            }

            AddButton(
                $"{GetEquipmentSlotName(slot)} 해제",
                () =>
                {
                    runtime.TryUnequipEquipment(member, capturedSlot, out statusMessage);
                    Render();
                },
                JourneyButtonStyle.Action);
        }

        foreach (ExpeditionEquipmentDefinition definition in runtime.GetEquipmentDefinitions()
            .Where(definition => definition != null
                && !string.IsNullOrWhiteSpace(definition.id)
                && runtime.GetAvailableEquipmentCount(definition.id) > 0)
            .OrderBy(definition => definition.slot)
            .ThenBy(definition => definition.displayName, StringComparer.Ordinal))
        {
            ExpeditionEquipmentDefinition captured = definition;
            AddButton(
                $"{GetEquipmentSlotName(definition.slot)} 장착: {definition.displayName} x{runtime.GetAvailableEquipmentCount(definition.id)}",
                () =>
                {
                    runtime.TryEquipEquipment(member, captured.id, out statusMessage);
                    Render();
                },
                JourneyButtonStyle.Supply);
        }
    }

    private string BuildEquipmentDetail(OffenseTargetSnapshot target)
    {
        if (runtime == null)
        {
            return string.Empty;
        }

        List<string> lines = new List<string> { "장비" };

        foreach (CharacterActor member in selectedMembers)
        {
            string readiness = OffenseExpeditionService.CanJoinExpedition(member, out string reason)
                ? "출정 가능"
                : $"출정 불가: {reason}";
            ExpeditionEquipmentDefinition weapon = runtime.TryGetEquippedEquipment(
                member,
                ExpeditionEquipmentSlot.Weapon,
                out ExpeditionEquipmentDefinition equippedWeapon)
                    ? equippedWeapon
                    : null;
            ExpeditionEquipmentDefinition armor = runtime.TryGetEquippedEquipment(
                member,
                ExpeditionEquipmentSlot.Armor,
                out ExpeditionEquipmentDefinition equippedArmor)
                    ? equippedArmor
                    : null;
            ExpeditionEquipmentStatBlock bonuses = runtime.GetEquipmentBonuses(member);
            lines.Add(
                $"{GetActorName(member)} - 무기 {GetEquipmentName(weapon)}, 방어구 {GetEquipmentName(armor)}"
                + $" / {FormatEquipmentStats(bonuses)} / {readiness}");
        }

        if (target != null && selectedMembers.Count < target.requiredMembers)
        {
            lines.Add($"인원 부족: {selectedMembers.Count}/{target.requiredMembers}");
        }

        string inventory = string.Join(", ", runtime.GetEquipmentDefinitions()
            .Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.id))
            .Select(definition =>
                $"{definition.displayName} {runtime.GetAvailableEquipmentCount(definition.id)}/{GetOwnedEquipmentCount(definition.id)}"));
        lines.Add(string.IsNullOrWhiteSpace(inventory) ? "재고 없음" : $"재고: {inventory}");

        string queue = string.Join(", ", runtime.GetEquipmentCraftQueue()
            .Where(order => order != null && !string.IsNullOrWhiteSpace(order.equipmentId))
            .Select(order =>
            {
                string name = runtime.GetEquipmentDefinitions()
                    .FirstOrDefault(definition => string.Equals(definition.id, order.equipmentId, StringComparison.Ordinal))
                    ?.displayName ?? order.equipmentId;
                return $"{name} {order.remainingSeconds:0.#}s";
            }));
        if (!string.IsNullOrWhiteSpace(queue))
        {
            lines.Add($"제작 대기: {queue}");
        }

        return "\n\n" + string.Join("\n", lines);
    }

    private int GetOwnedEquipmentCount(string equipmentId)
    {
        IReadOnlyDictionary<string, int> inventory = runtime?.GetEquipmentInventory();
        return inventory != null && inventory.TryGetValue(equipmentId, out int count)
            ? count
            : 0;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (runtime != null) runtime.StateChanged -= Render;
    }

    private void RenderJourney(OffenseExpeditionRun expedition)
    {
        OffenseRouteNode current = expedition.CurrentNode;
        headerText.text = $"{expedition.Target.title}  ·  {GetPhaseName(expedition.Phase)}";
        detailText.text = BuildJourneyDetail(expedition, current);

        if (expedition.Phase == OffenseExpeditionPhase.ChoosingRoute)
        {
            foreach (OffenseRouteNode node in expedition.GetAvailableRouteNodes())
            {
                OffenseRouteNode captured = node;
                AddButton($"{GetNodeIcon(node.Kind)} {node.Title}", () =>
                {
                    statusMessage = runtime.TryChooseRouteNode(
                        expedition.ExpeditionId,
                        captured.Id,
                        out string message)
                            ? message
                            : message;
                    Render();
                }, node.IsBoss ? JourneyButtonStyle.Danger : JourneyButtonStyle.Route);
            }

            if (expedition.Supplies.Get(OffenseSupplyType.Rations) > 0)
            {
                AddButton("식량 나누기  ·  스트레스 회복", () =>
                    UseSupply(expedition, OffenseSupplyType.Rations, -1), JourneyButtonStyle.Supply);
            }
            if (expedition.Supplies.Get(OffenseSupplyType.ManaLantern) > 0
                && expedition.Light < 100f)
            {
                AddButton("마력등 밝히기  ·  조명 +35", () =>
                    UseSupply(expedition, OffenseSupplyType.ManaLantern, -1), JourneyButtonStyle.Supply);
            }
            if (expedition.Supplies.Get(OffenseSupplyType.Medicine) > 0)
            {
                for (int index = 0; index < expedition.MemberStates.Count; index++)
                {
                    int capturedIndex = index;
                    OffenseExpeditionMemberState member = expedition.MemberStates[index];
                    if (!member.IsAlive || member.Actor.CurrentHealth >= member.Actor.MaxHealth) continue;
                    AddButton($"{GetActorName(member.Actor)} 치료", () =>
                        UseSupply(expedition, OffenseSupplyType.Medicine, capturedIndex), JourneyButtonStyle.Supply);
                }
            }

            for (int index = 0; index + 1 < expedition.MemberStates.Count; index++)
            {
                int capturedIndex = index;
                AddButton(
                    $"{OffenseFormationUtility.GetDisplayName(expedition.MemberStates[index].Formation)}"
                    + $" ↔ {OffenseFormationUtility.GetDisplayName(expedition.MemberStates[index + 1].Formation)}",
                    () =>
                    {
                        runtime.TrySwapFormation(
                            expedition.ExpeditionId,
                            capturedIndex,
                            capturedIndex + 1,
                            out statusMessage);
                        Render();
                    }, JourneyButtonStyle.Action);
            }

            AddButton("원정 철수", () =>
            {
                runtime.TryRetreat(expedition.ExpeditionId, out statusMessage);
                Render();
            }, JourneyButtonStyle.Danger);
        }
        else if (expedition.Phase == OffenseExpeditionPhase.ResolvingNode)
        {
            if (current?.Kind == OffenseRouteNodeKind.Cache)
            {
                AddNodeResolutionButton(expedition, "보급고 수색", false);
            }
            else
            {
                string supplyChoice = current?.Kind == OffenseRouteNodeKind.Camp
                    ? "야영하기  ·  식량 2"
                    : "원정 도구 사용";
                string riskChoice = current?.Kind == OffenseRouteNodeKind.Camp
                    ? "쉬지 않고 전진"
                    : "위험 감수";
                AddNodeResolutionButton(expedition, supplyChoice, true);
                AddNodeResolutionButton(expedition, riskChoice, false);
            }
        }

        AddButton("닫기", Hide, JourneyButtonStyle.Close);
    }

    private void AddNodeResolutionButton(
        OffenseExpeditionRun expedition,
        string label,
        bool useSupply)
    {
        AddButton(label, () =>
        {
            runtime.TryResolveCurrentNode(
                expedition.ExpeditionId,
                useSupply,
                out _,
                out statusMessage);
            Render();
        }, useSupply ? JourneyButtonStyle.Supply : JourneyButtonStyle.Route);
    }

    private void UseSupply(
        OffenseExpeditionRun expedition,
        OffenseSupplyType type,
        int memberIndex)
    {
        runtime.TryUseSupply(expedition.ExpeditionId, type, memberIndex, out statusMessage);
        Render();
    }

    private void IncrementSupply(
        OffenseSupplyType type,
        OffensePreparationSnapshot preparation)
    {
        int current = selectedSupplies[type];
        int totalWithoutCurrent = selectedSupplies.Values.Sum() - current;
        int maximum = Mathf.Min(
            preparation.GetAvailable(type),
            Mathf.Max(0, preparation.Preparation.SupplyCapacity - totalWithoutCurrent));
        selectedSupplies[type] = current >= maximum ? 0 : current + 1;
        statusMessage = string.Empty;
        Render();
    }

    private void ResetSupplies(bool render = true)
    {
        foreach (OffenseSupplyType type in selectedSupplies.Keys.ToArray())
        {
            selectedSupplies[type] = 0;
        }
        if (render) Render();
    }

    private void AddButton(
        string label,
        Action callback,
        JourneyButtonStyle style = JourneyButtonStyle.Action)
    {
        GameObject button = RequireButtonFactory().CreateButton(
            memberButtonRoot,
            label,
            15f,
            callback);
        button.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 38f;
        StyleJourneyButton(button, style);
        spawnedButtons.Add(button);
    }

    private static void StyleJourneyButton(GameObject buttonObject, JourneyButtonStyle style)
    {
        if (buttonObject == null) return;
        UnityEngine.UI.Button button = buttonObject.GetComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Image image = buttonObject.GetComponent<UnityEngine.UI.Image>();
        Color baseColor = style switch
        {
            JourneyButtonStyle.Route => new Color32(96, 45, 42, 255),
            JourneyButtonStyle.Supply => new Color32(42, 72, 60, 255),
            JourneyButtonStyle.Danger => new Color32(122, 39, 40, 255),
            JourneyButtonStyle.Close => new Color32(40, 39, 44, 255),
            _ => new Color32(54, 52, 57, 255)
        };
        image.color = baseColor;
        button.colors = new UnityEngine.UI.ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1.15f, 1.12f, 1.05f, 1f),
            pressedColor = new Color(0.72f, 0.7f, 0.68f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.7f),
            colorMultiplier = 1f,
            fadeDuration = 0.06f
        };
        UnityEngine.UI.Outline outline = buttonObject.GetComponent<UnityEngine.UI.Outline>()
            ?? buttonObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = style is JourneyButtonStyle.Route or JourneyButtonStyle.Danger
            ? new Color32(188, 151, 83, 220)
            : new Color32(102, 96, 105, 180);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private string BuildJourneyDetail(OffenseExpeditionRun expedition, OffenseRouteNode current)
    {
        List<string> lines = new List<string>
        {
            $"현재 위치  {current?.Title ?? "입구"}",
            $"조명  {expedition.Light:0}/100  ·  전리품 {expedition.CarriedStock.Values.Sum()}",
            $"보급  식량 {expedition.Supplies.Get(OffenseSupplyType.Rations)}"
                + $"  치료약 {expedition.Supplies.Get(OffenseSupplyType.Medicine)}"
                + $"  도구 {expedition.Supplies.Get(OffenseSupplyType.Tools)}"
                + $"  마력등 {expedition.Supplies.Get(OffenseSupplyType.ManaLantern)}",
            string.Empty,
            "원정대"
        };
        foreach (OffenseExpeditionMemberState member in expedition.MemberStates.OrderBy(value => value.Formation))
        {
            lines.Add(
                $"{OffenseFormationUtility.GetDisplayName(member.Formation)}  Lv.{member.Actor.Progression?.Level ?? 1}  {GetActorName(member.Actor)}"
                + $"  체력 {member.Actor.CurrentHealth:0}/{member.Actor.MaxHealth:0}"
                + $"  스트레스 {member.Stress:0}");
        }

        lines.Add(string.Empty);
        lines.Add("경로");
        int currentDepth = current?.Depth ?? 0;
        foreach (IGrouping<int, OffenseRouteNode> depth in expedition.Route.Nodes
            .OrderBy(node => node.Depth)
            .ThenBy(node => node.Lane)
            .GroupBy(node => node.Depth))
        {
            lines.Add(string.Join("  /  ", depth.Select(node =>
            {
                if (expedition.CompletedNodeIds.Contains(node.Id)) return $"완료 {node.Title}";
                if (string.Equals(node.Id, expedition.CurrentNodeId, StringComparison.Ordinal)) return $"현재 {node.Title}";
                if (node.Depth > currentDepth + 1 + expedition.Preparation.Scouting) return "미확인";
                return node.Title;
            })));
        }

        if (!string.IsNullOrWhiteSpace(current?.Description))
        {
            lines.Add(string.Empty);
            lines.Add(current.Description);
        }
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            lines.Add(string.Empty);
            lines.Add(statusMessage);
        }
        return string.Join("\n", lines);
    }

    private static string BuildPreparationSources(OffenseExpeditionPreparation preparation)
    {
        return preparation.SourceSummaries.Count > 0
            ? $"\n지원 시설: {string.Join(", ", preparation.SourceSummaries)}"
            : "\n지원 시설 없음";
    }

    private static string GetPhaseName(OffenseExpeditionPhase phase)
    {
        return phase switch
        {
            OffenseExpeditionPhase.ChoosingRoute => "경로 선택",
            OffenseExpeditionPhase.ResolvingNode => "현장 판단",
            OffenseExpeditionPhase.InBattle => "교전 중",
            OffenseExpeditionPhase.Completed => "완수",
            OffenseExpeditionPhase.Retreated => "철수",
            _ => "패배"
        };
    }

    private static string GetNodeIcon(OffenseRouteNodeKind kind)
    {
        return kind switch
        {
            OffenseRouteNodeKind.Battle => "교전",
            OffenseRouteNodeKind.Event => "사건",
            OffenseRouteNodeKind.Camp => "야영",
            OffenseRouteNodeKind.Cache => "보급",
            OffenseRouteNodeKind.Boss => "목표",
            _ => "입구"
        };
    }

    private static string BuildMemberLabel(CharacterActor member)
    {
        if (member == null)
        {
            return "알 수 없음";
        }

        member.EnsureRuntimeState();
        CharacterIdentity identity = member.Identity;
        string name = identity != null ? identity.DisplayName : member.name;
        string speciesTag = identity != null ? identity.SpeciesTag : string.Empty;
        int level = member.Progression != null ? member.Progression.Level : 1;
        return $"Lv.{level} / {name} / {speciesTag} / 체력 {member.CurrentHealth:0}/{member.MaxHealth:0}";
    }

    private static string GetEquipmentSlotName(ExpeditionEquipmentSlot slot)
    {
        return slot == ExpeditionEquipmentSlot.Weapon ? "무기" : "방어구";
    }

    private static string GetEquipmentName(ExpeditionEquipmentDefinition definition)
    {
        return definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
            ? definition.displayName
            : "없음";
    }

    private static string FormatEquipmentStats(ExpeditionEquipmentStatBlock stats)
    {
        if (stats == null)
        {
            return "보정 없음";
        }

        List<string> parts = new List<string>();
        AddStat(parts, "체력", stats.maxHealth);
        AddStat(parts, "공격", stats.attack);
        AddStat(parts, "근력", stats.strength);
        AddStat(parts, "맷집", stats.toughness);
        AddStat(parts, "민첩", stats.dexterity);
        AddStat(parts, "이동", stats.moveSpeed);
        return parts.Count > 0 ? string.Join(" ", parts) : "보정 없음";
    }

    private static void AddStat(ICollection<string> parts, string label, int value)
    {
        if (value != 0)
        {
            parts.Add($"{label} {value:+#;-#;0}");
        }
    }

    private static string GetActorName(CharacterActor actor)
    {
        actor?.EnsureRuntimeState();
        return actor != null && actor.Identity != null
            ? actor.Identity.DisplayName
            : actor != null ? actor.name : "대원";
    }

    private void EnsureView()
    {
        if (headerText != null && detailText != null && memberButtonRoot != null) return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        headerText = texts.FirstOrDefault((text) => text.name == "OffenseExpeditionHeader");
        detailText = texts.FirstOrDefault((text) => text.name == "OffenseExpeditionDetail");
        memberButtonRoot = GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault((rect) => rect.name == "OffenseExpeditionMembers");
    }

    private void ClearButtons()
    {
        foreach (GameObject button in spawnedButtons)
        {
            if (button != null)
            {
                RequireButtonFactory().Release(button);
            }
        }

        spawnedButtons.Clear();
    }

    internal void BindGeneratedView(
        TMP_Text headerText,
        TMP_Text detailText,
        RectTransform memberButtonRoot)
    {
        this.headerText = headerText != null
            ? headerText
            : throw new ArgumentNullException(nameof(headerText));
        this.detailText = detailText != null
            ? detailText
            : throw new ArgumentNullException(nameof(detailText));
        this.memberButtonRoot = memberButtonRoot != null
            ? memberButtonRoot
            : throw new ArgumentNullException(nameof(memberButtonRoot));
    }

    private IOffensePanelButtonFactory RequireButtonFactory()
    {
        return buttonFactory
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseExpeditionPanel)} requires {nameof(IOffensePanelButtonFactory)} binding.");
    }
}
