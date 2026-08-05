using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class OffenseExpeditionRun
{
    private readonly List<CharacterActor> members;
    private readonly List<CharacterActor> protectedRescueMembers =
        new List<CharacterActor>();
    private readonly IReadOnlyList<CharacterActor> membersView;
    private readonly IReadOnlyList<CharacterActor> protectedRescueMembersView;
    private readonly List<OffenseExpeditionMemberState> memberStates;
    private readonly IReadOnlyList<OffenseExpeditionMemberState> memberStatesView;
    private readonly HashSet<string> completedNodeIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<StockCategory, int> carriedStock = new Dictionary<StockCategory, int>();
    private readonly IReadOnlyDictionary<StockCategory, int> carriedStockView;

    public OffenseExpeditionRun(
        string expeditionId,
        OffenseTargetDefinition target,
        IEnumerable<CharacterActor> members,
        float totalPower)
        : this(
            expeditionId,
            target,
            members,
            totalPower,
            target != null ? Mathf.Max(1f, target.durationSeconds) : 1f,
            null,
            null,
            null)
    {
    }

    public OffenseExpeditionRun(
        string expeditionId,
        OffenseTargetDefinition target,
        IEnumerable<CharacterActor> members,
        float totalPower,
        float remainingSeconds)
        : this(
            expeditionId,
            target,
            members,
            totalPower,
            remainingSeconds,
            null,
            null,
            null)
    {
    }

    public OffenseExpeditionRun(
        string expeditionId,
        OffenseTargetDefinition target,
        IEnumerable<CharacterActor> members,
        float totalPower,
        float remainingSeconds,
        OffenseRouteGraph route,
        OffenseSupplyLoadout supplies,
        OffenseExpeditionPreparation preparation)
    {
        ExpeditionId = string.IsNullOrWhiteSpace(expeditionId)
            ? Guid.NewGuid().ToString("N")
            : expeditionId;
        Target = target;
        this.members = members?.Where((member) => member != null).Distinct().ToList()
            ?? new List<CharacterActor>();
        membersView = this.members.AsReadOnly();
        protectedRescueMembersView = protectedRescueMembers.AsReadOnly();
        memberStates = this.members
            .Take(5)
            .Select((member, index) => new OffenseExpeditionMemberState(
                member,
                (OffenseFormationSlot)Mathf.Clamp(index / 2, 0, 2)))
            .ToList();
        memberStatesView = memberStates.AsReadOnly();
        TotalPower = Mathf.Max(0f, totalPower);
        TotalDurationSeconds = target != null ? Mathf.Max(1f, target.durationSeconds) : 1f;
        RemainingSeconds = Mathf.Clamp(remainingSeconds, 0f, TotalDurationSeconds);
        Route = route ?? OffenseRouteGenerator.Create(target);
        Supplies = supplies ?? new OffenseSupplyLoadout();
        Preparation = preparation ?? new OffenseExpeditionPreparation();
        FieldFunds = Preparation.FieldFunds;
        Light = Preparation.StartingLight;
        CurrentNodeId = Route.EntranceNodeId;
        completedNodeIds.Add(Route.EntranceNodeId);
        carriedStockView = carriedStock;
        Phase = OffenseExpeditionPhase.ChoosingRoute;
    }

    public string ExpeditionId { get; }
    public OffenseTargetDefinition Target { get; private set; }
    public IReadOnlyList<CharacterActor> MemberActors => membersView;
    public IReadOnlyList<CharacterActor> ProtectedRescueActors =>
        protectedRescueMembersView;
    public IReadOnlyList<OffenseExpeditionMemberState> MemberStates => memberStatesView;
    public float TotalPower { get; }
    public float RemainingSeconds { get; private set; }
    public float TotalDurationSeconds { get; }
    public OffenseRouteGraph Route { get; }
    public OffenseSupplyLoadout Supplies { get; }
    public OffenseExpeditionPreparation Preparation { get; }
    public OffenseExpeditionPhase Phase { get; private set; }
    public string CurrentNodeId { get; private set; }
    public float Light { get; private set; }
    public IReadOnlyCollection<string> CompletedNodeIds => completedNodeIds;
    public IReadOnlyDictionary<StockCategory, int> CarriedStock => carriedStockView;
    public bool IsComplete => Phase is OffenseExpeditionPhase.Completed
        or OffenseExpeditionPhase.Retreated
        or OffenseExpeditionPhase.Defeated;
    public bool UsesWorldTravel => !string.IsNullOrWhiteSpace(WorldSiteId);
    public string WorldSiteId { get; private set; } = string.Empty;
    public bool WorldObjectiveCompleted { get; private set; }
    public bool WorldObjectiveBattleActive { get; private set; }
    public bool DepartureCompleted { get; private set; }
    public int FieldFunds { get; private set; }
    public bool FieldFundsReturned { get; private set; }
    public OffenseRouteNode CurrentNode => Route.TryGetNode(CurrentNodeId, out OffenseRouteNode node)
        ? node
        : null;

    public void MergeProtectedRescueMembers(IEnumerable<CharacterActor> rescued)
    {
        foreach (CharacterActor actor in rescued ?? Array.Empty<CharacterActor>())
        {
            if (actor != null
                && !members.Contains(actor)
                && !protectedRescueMembers.Contains(actor))
            {
                protectedRescueMembers.Add(actor);
            }
        }
    }

    public void BeginRescueReturn()
    {
        WorldObjectiveCompleted = true;
        WorldObjectiveBattleActive = false;
        Phase = OffenseExpeditionPhase.Returning;
    }

    public IReadOnlyList<OffenseRouteNode> GetAvailableRouteNodes()
    {
        return Phase == OffenseExpeditionPhase.ChoosingRoute
            ? Route.GetNextNodes(CurrentNodeId)
            : Array.Empty<OffenseRouteNode>();
    }

    public bool TryEnterNode(string nodeId, out string message)
    {
        if (Phase != OffenseExpeditionPhase.ChoosingRoute)
        {
            message = "현재는 다음 경로를 선택할 수 없습니다.";
            return false;
        }

        OffenseRouteNode node = Route.GetNextNodes(CurrentNodeId)
            .FirstOrDefault(candidate => string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        if (node == null)
        {
            message = "현재 위치에서 이어지지 않는 경로입니다.";
            return false;
        }

        CurrentNodeId = node.Id;
        if (!Supplies.TryConsume(OffenseSupplyType.Rations, 1))
        {
            ApplyStressToSurvivors(6f);
        }

        Light = Mathf.Clamp(Light - (18f + node.DangerMultiplier * 5f), 0f, 100f);
        if (Light <= 20f)
        {
            ApplyStressToSurvivors(4f);
        }

        Phase = node.StartsBattle
            ? OffenseExpeditionPhase.InBattle
            : OffenseExpeditionPhase.ResolvingNode;
        message = $"{node.Title}에 진입했습니다.";
        return true;
    }

    public bool TryResolveCurrentNode(
        bool useSupply,
        out OffenseExpeditionNodeResult result,
        out string message)
    {
        result = null;
        OffenseRouteNode node = CurrentNode;
        if (Phase != OffenseExpeditionPhase.ResolvingNode || node == null)
        {
            message = "선택으로 해결할 원정 노드가 아닙니다.";
            return false;
        }

        bool usedSupply = false;
        bool gainedLoot = false;
        string resultMessage;
        switch (node.Kind)
        {
            case OffenseRouteNodeKind.Event:
                if (useSupply)
                {
                    if (!Supplies.TryConsume(OffenseSupplyType.Tools, 1))
                    {
                        message = "사용할 원정 도구가 없습니다.";
                        return false;
                    }

                    usedSupply = true;
                    RecoverStressForSurvivors(5f);
                    AddCarriedStock(StockCategory.General, 3 + Mathf.Max(1, Target?.campaignOrder ?? 1));
                    gainedLoot = true;
                    resultMessage = "도구로 위험을 걷어내고 숨겨진 물자를 챙겼습니다.";
                }
                else
                {
                    ApplyStressToSurvivors(10f);
                    resultMessage = "위험을 감수해 통과했습니다. 원정대의 스트레스가 올랐습니다.";
                }
                break;
            case OffenseRouteNodeKind.Camp:
                if (useSupply)
                {
                    if (!Supplies.TryConsume(OffenseSupplyType.Rations, 2))
                    {
                        message = "야영에는 식량 2개가 필요합니다.";
                        return false;
                    }

                    usedSupply = true;
                    foreach (OffenseExpeditionMemberState member in memberStates.Where(value => value.IsAlive))
                    {
                        member.Actor.Heal(member.Actor.MaxHealth * Preparation.CampHealRatio);
                        member.RecoverStress(Preparation.CampStressRecovery);
                    }
                    resultMessage = "야영을 마치고 체력과 스트레스를 회복했습니다.";
                }
                else
                {
                    ApplyStressToSurvivors(5f);
                    resultMessage = "쉬지 않고 전진했습니다.";
                }
                break;
            case OffenseRouteNodeKind.Cache:
                AddCarriedStock(ResolveCacheCategory(), 5 + Mathf.Max(1, Target?.campaignOrder ?? 1) * 2);
                gainedLoot = true;
                resultMessage = "보급고에서 운반할 전리품을 확보했습니다.";
                break;
            default:
                message = "이 노드는 별도 선택으로 해결할 수 없습니다.";
                return false;
        }

        CompleteCurrentNode();
        result = new OffenseExpeditionNodeResult(resultMessage, usedSupply, gainedLoot);
        message = resultMessage;
        return true;
    }

    public bool TryUseSupply(OffenseSupplyType type, int memberIndex, out string message)
    {
        if (Phase != OffenseExpeditionPhase.ChoosingRoute)
        {
            message = "경로를 선택하는 동안에만 보급품을 정비할 수 있습니다.";
            return false;
        }

        switch (type)
        {
            case OffenseSupplyType.Rations:
                if (!Supplies.TryConsume(type, 1))
                {
                    message = "식량이 없습니다.";
                    return false;
                }
                RecoverStressForSurvivors(8f);
                message = "식량을 나눠 먹어 스트레스를 낮췄습니다.";
                return true;
            case OffenseSupplyType.Medicine:
                if (memberIndex < 0 || memberIndex >= memberStates.Count || !memberStates[memberIndex].IsAlive)
                {
                    message = "치료할 대원을 선택해야 합니다.";
                    return false;
                }
                if (!Supplies.TryConsume(type, 1))
                {
                    message = "치료약이 없습니다.";
                    return false;
                }
                CharacterActor actor = memberStates[memberIndex].Actor;
                actor.Heal(actor.MaxHealth * Preparation.MedicineHealRatio);
                message = $"{GetMemberName(actor)}을 치료했습니다.";
                return true;
            case OffenseSupplyType.ManaLantern:
                if (!Supplies.TryConsume(type, 1))
                {
                    message = "마력등이 없습니다.";
                    return false;
                }
                Light = Mathf.Clamp(Light + 35f, 0f, 100f);
                message = "마력등을 밝혀 시야를 회복했습니다.";
                return true;
            default:
                message = "원정 도구는 사건 현장에서 사용합니다.";
                return false;
        }
    }

    public bool TrySwapFormation(int firstIndex, int secondIndex, out string message)
    {
        if (Phase != OffenseExpeditionPhase.ChoosingRoute
            || firstIndex < 0 || firstIndex >= memberStates.Count
            || secondIndex < 0 || secondIndex >= memberStates.Count
            || firstIndex == secondIndex)
        {
            message = "지금은 해당 진형을 바꿀 수 없습니다.";
            return false;
        }

        OffenseFormationSlot first = memberStates[firstIndex].Formation;
        memberStates[firstIndex].Formation = memberStates[secondIndex].Formation;
        memberStates[secondIndex].Formation = first;
        memberStates.Sort((left, right) => left.Formation.CompareTo(right.Formation));
        members.Clear();
        members.AddRange(memberStates.Select(state => state.Actor));
        message = "원정대 진형을 변경했습니다.";
        return true;
    }

    public void RecordBattleMemberResult(CharacterActor actor, float damageTaken, bool survived)
    {
        OffenseExpeditionMemberState member = memberStates.FirstOrDefault(value => value.Actor == actor);
        if (member == null) return;
        member.RecordDamage(damageTaken);
        member.AddStress(survived ? 8f + damageTaken / Mathf.Max(1f, actor.MaxHealth) * 20f : 100f);
    }

    public void CompleteBattleNode(bool victory)
    {
        OffenseRouteNode node = CurrentNode;
        if (Phase != OffenseExpeditionPhase.InBattle || node == null) return;
        if (!victory)
        {
            Phase = OffenseExpeditionPhase.Defeated;
            return;
        }

        if (node.Kind == OffenseRouteNodeKind.Battle)
        {
            AddCarriedStock(StockCategory.Weapon, 2 + Mathf.Max(1, Target?.campaignOrder ?? 1));
        }

        completedNodeIds.Add(node.Id);
        Phase = node.IsBoss
            ? OffenseExpeditionPhase.Completed
            : OffenseExpeditionPhase.ChoosingRoute;
    }

    public void BeginWorldTravel(string siteId)
    {
        WorldSiteId = siteId ?? string.Empty;
        WorldObjectiveCompleted = false;
        WorldObjectiveBattleActive = false;
        Phase = OffenseExpeditionPhase.Traveling;
    }

    public void MarkDepartureCompleted()
    {
        DepartureCompleted = true;
    }

    public bool RetargetWorldObjective(OffenseTargetDefinition target)
    {
        if (target == null
            || !target.IsValid
            || !UsesWorldTravel
            || Phase is OffenseExpeditionPhase.InBattle
                or OffenseExpeditionPhase.AwaitingDecision
                or OffenseExpeditionPhase.Completed
                or OffenseExpeditionPhase.Defeated
                or OffenseExpeditionPhase.Retreated)
        {
            return false;
        }

        Target = target;
        WorldSiteId = target.id;
        WorldObjectiveCompleted = false;
        WorldObjectiveBattleActive = false;
        Phase = OffenseExpeditionPhase.Traveling;
        return true;
    }

    public void AdjustStress(float amount)
    {
        if (amount >= 0f)
        {
            ApplyStressToSurvivors(amount);
        }
        else
        {
            RecoverStressForSurvivors(-amount);
        }
    }

    public bool ApplyEventInjury(
        float maxHealthRatio,
        int deterministicRoll,
        bool nonLethal)
    {
        CharacterActor[] candidates = memberStates
            .Where(member => member.IsAlive && member.Actor != null)
            .Select(member => member.Actor)
            .OrderBy(actor => actor.Identity?.PersistentId ?? actor.name)
            .ToArray();
        if (candidates.Length == 0)
        {
            return false;
        }

        int index = (int)((uint)deterministicRoll % (uint)candidates.Length);
        CharacterActor victim = candidates[index];
        float damage = victim.MaxHealth * Mathf.Clamp(maxHealthRatio, 0f, 0.95f);
        if (nonLethal)
        {
            damage = Mathf.Min(damage, Mathf.Max(0f, victim.CurrentHealth - 1f));
        }

        if (damage <= 0f)
        {
            return false;
        }

        victim.ApplyDamage(damage, "원정 사건 부상");
        OffenseExpeditionMemberState member = memberStates.FirstOrDefault(
            state => state.Actor == victim);
        member?.RecordDamage(damage);
        return true;
    }

    public bool HealMostInjured(float maxHealthRatio)
    {
        CharacterActor actor = memberStates
            .Where(member => member.IsAlive && member.Actor != null)
            .Select(member => member.Actor)
            .OrderBy(candidate =>
                candidate.CurrentHealth / Mathf.Max(1f, candidate.MaxHealth))
            .ThenBy(candidate =>
                candidate.Identity?.PersistentId ?? candidate.name)
            .FirstOrDefault();
        if (actor == null)
        {
            return false;
        }

        actor.Heal(actor.MaxHealth * Mathf.Clamp01(maxHealthRatio));
        return true;
    }

    public int GetCarriedStock(StockCategory category)
    {
        return carriedStock.TryGetValue(category, out int amount)
            ? amount
            : 0;
    }

    public void AddCarriedLoot(StockCategory category, int amount)
    {
        AddCarriedStock(category, amount);
    }

    public bool TryRemoveCarriedLoot(StockCategory category, int amount)
    {
        int removal = Mathf.Max(0, amount);
        if (removal == 0)
        {
            return true;
        }

        int current = GetCarriedStock(category);
        if (current < removal)
        {
            return false;
        }

        int remaining = current - removal;
        if (remaining > 0)
        {
            carriedStock[category] = remaining;
        }
        else
        {
            carriedStock.Remove(category);
        }

        return true;
    }

    public bool CanSpendFieldFunds(int amount)
    {
        return FieldFunds >= Mathf.Max(0, amount);
    }

    public bool TrySpendFieldFunds(int amount)
    {
        int cost = Mathf.Max(0, amount);
        if (FieldFunds < cost)
        {
            return false;
        }

        FieldFunds -= cost;
        return true;
    }

    public void AddFieldFunds(int amount)
    {
        FieldFunds += Mathf.Max(0, amount);
    }

    public int TakeReturningFieldFunds()
    {
        if (FieldFundsReturned)
        {
            return 0;
        }

        FieldFundsReturned = true;
        int returning = Mathf.Max(0, FieldFunds);
        FieldFunds = 0;
        return returning;
    }

    public void RestoreFieldFunds(int amount, bool returned)
    {
        FieldFunds = Mathf.Max(0, amount);
        FieldFundsReturned = returned;
    }

    public bool BeginWorldBattle(bool objectiveBattle = true)
    {
        if (!UsesWorldTravel
            || Phase is OffenseExpeditionPhase.Completed
                or OffenseExpeditionPhase.Defeated
                or OffenseExpeditionPhase.Retreated)
        {
            return false;
        }

        WorldObjectiveBattleActive = objectiveBattle;
        Phase = OffenseExpeditionPhase.InBattle;
        return true;
    }

    public void CompleteWorldBattle(bool victory)
    {
        if (!UsesWorldTravel || Phase != OffenseExpeditionPhase.InBattle)
        {
            return;
        }

        if (!victory)
        {
            WorldObjectiveBattleActive = false;
            Phase = OffenseExpeditionPhase.Defeated;
            return;
        }

        if (WorldObjectiveBattleActive)
        {
            WorldObjectiveCompleted = true;
            Phase = OffenseExpeditionPhase.Returning;
        }
        else
        {
            Phase = WorldObjectiveCompleted
                ? OffenseExpeditionPhase.Returning
                : OffenseExpeditionPhase.Traveling;
        }

        WorldObjectiveBattleActive = false;
    }

    public void SetDecisionPaused(bool paused)
    {
        if (!UsesWorldTravel || IsComplete || Phase == OffenseExpeditionPhase.InBattle)
        {
            return;
        }

        Phase = paused
            ? OffenseExpeditionPhase.AwaitingDecision
            : WorldObjectiveCompleted
                ? OffenseExpeditionPhase.Returning
                : OffenseExpeditionPhase.Traveling;
    }

    public bool Retreat(out string message)
    {
        if (IsComplete)
        {
            message = "이미 끝난 원정입니다.";
            return false;
        }

        Phase = OffenseExpeditionPhase.Retreated;
        message = "운반 중인 전리품을 지키며 던전으로 철수합니다.";
        return true;
    }

    internal void Tick(float deltaTime)
    {
        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Mathf.Max(0f, deltaTime));
    }

    public void RestoreJourneyState(
        OffenseExpeditionPhase phase,
        string currentNodeId,
        float light,
        IEnumerable<string> completedNodes,
        IReadOnlyDictionary<StockCategory, int> restoredCarriedStock)
    {
        if (Route.TryGetNode(currentNodeId, out _))
        {
            CurrentNodeId = currentNodeId;
        }

        Phase = phase;
        Light = Mathf.Clamp(light, 0f, 100f);
        completedNodeIds.Clear();
        completedNodeIds.Add(Route.EntranceNodeId);
        foreach (string nodeId in completedNodes ?? Array.Empty<string>())
        {
            if (Route.TryGetNode(nodeId, out _)) completedNodeIds.Add(nodeId);
        }

        carriedStock.Clear();
        if (restoredCarriedStock == null) return;
        foreach (KeyValuePair<StockCategory, int> pair in restoredCarriedStock)
        {
            if (pair.Value > 0) carriedStock[pair.Key] = pair.Value;
        }
    }

    public void RestoreStrategicJourneyState(
        string siteId,
        bool objectiveCompleted,
        bool objectiveBattleActive,
        OffenseExpeditionPhase phase)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return;
        }

        WorldSiteId = siteId;
        WorldObjectiveCompleted = objectiveCompleted;
        WorldObjectiveBattleActive = objectiveBattleActive
            && phase == OffenseExpeditionPhase.InBattle;
        Phase = phase;
    }

    public void RestoreDepartureState(bool completed)
    {
        DepartureCompleted = completed;
    }

    private void CompleteCurrentNode()
    {
        completedNodeIds.Add(CurrentNodeId);
        Phase = OffenseExpeditionPhase.ChoosingRoute;
    }

    private void ApplyStressToSurvivors(float amount)
    {
        foreach (OffenseExpeditionMemberState member in memberStates.Where(value => value.IsAlive))
        {
            member.AddStress(amount);
        }
    }

    private void RecoverStressForSurvivors(float amount)
    {
        foreach (OffenseExpeditionMemberState member in memberStates.Where(value => value.IsAlive))
        {
            member.RecoverStress(amount);
        }
    }

    private void AddCarriedStock(StockCategory category, int amount)
    {
        if (amount <= 0) return;
        carriedStock[category] = carriedStock.TryGetValue(category, out int current)
            ? current + amount
            : amount;
    }

    private StockCategory ResolveCacheCategory()
    {
        int categoryIndex = Mathf.Max(1, Target?.campaignOrder ?? 1) % 3;
        return categoryIndex switch
        {
            0 => StockCategory.Mana,
            1 => StockCategory.Food,
            _ => StockCategory.General
        };
    }

    private static string GetMemberName(CharacterActor actor)
    {
        actor?.EnsureRuntimeState();
        return actor != null && actor.Identity != null
            ? actor.Identity.DisplayName
            : actor != null ? actor.name : "대원";
    }
}
