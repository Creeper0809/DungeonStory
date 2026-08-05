using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class OffenseDecisionEffectContext
{
    public OffenseDecisionEffectContext(
        OffenseExpeditionRun expedition,
        IOffenseTravelRuntime travel,
        IOffenseWorldSimulation world,
        IGameMoneyAccount money,
        int deterministicRoll,
        ICombatEquipmentRuntime equipment)
    {
        Expedition = expedition
            ?? throw new ArgumentNullException(nameof(expedition));
        Travel = travel ?? throw new ArgumentNullException(nameof(travel));
        World = world ?? throw new ArgumentNullException(nameof(world));
        Money = money ?? throw new ArgumentNullException(nameof(money));
        Equipment = equipment;
        DeterministicRoll = deterministicRoll;
    }

    public OffenseExpeditionRun Expedition { get; }
    public IOffenseTravelRuntime Travel { get; }
    public IOffenseWorldSimulation World { get; }
    public IGameMoneyAccount Money { get; }
    public ICombatEquipmentRuntime Equipment { get; }
    public int DeterministicRoll { get; }
    public bool StartsCombat { get; set; }
    public bool ForcesMovement { get; set; }
    public List<string> Results { get; } = new List<string>();
}

public interface IOffenseDecisionEffectHandler
{
    Type EffectType { get; }
    bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason);
    void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context);
}

public interface IOffenseDecisionEffectExecutor
{
    bool CanExecute(
        IReadOnlyList<OffenseDecisionEffectDefinition> effects,
        OffenseDecisionEffectContext context,
        out string reason);
    void Execute(
        IReadOnlyList<OffenseDecisionEffectDefinition> effects,
        OffenseDecisionEffectContext context);
}

public sealed class OffenseDecisionEffectExecutor :
    IOffenseDecisionEffectExecutor
{
    private readonly Dictionary<Type, IOffenseDecisionEffectHandler> handlers;

    public OffenseDecisionEffectExecutor(
        IReadOnlyList<IOffenseDecisionEffectHandler> registeredHandlers)
    {
        handlers = (registeredHandlers
                ?? Array.Empty<IOffenseDecisionEffectHandler>())
            .Where(handler => handler?.EffectType != null)
            .GroupBy(handler => handler.EffectType)
            .ToDictionary(
                group => group.Key,
                group => group.Count() == 1
                    ? group.Single()
                    : throw new InvalidOperationException(
                        $"Duplicate offense decision effect handler '{group.Key.Name}'."));
    }

    public bool CanExecute(
        IReadOnlyList<OffenseDecisionEffectDefinition> effects,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        foreach (OffenseDecisionEffectDefinition effect in
                 effects ?? Array.Empty<OffenseDecisionEffectDefinition>())
        {
            if (effect == null)
            {
                reason = "사건 효과 정의가 비어 있습니다.";
                return false;
            }

            if (!handlers.TryGetValue(effect.GetType(), out IOffenseDecisionEffectHandler handler))
            {
                reason = $"등록되지 않은 사건 효과입니다: {effect.GetType().Name}";
                return false;
            }

            if (!handler.CanExecute(effect, context, out reason))
            {
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    public void Execute(
        IReadOnlyList<OffenseDecisionEffectDefinition> effects,
        OffenseDecisionEffectContext context)
    {
        foreach (OffenseDecisionEffectDefinition effect in
                 effects ?? Array.Empty<OffenseDecisionEffectDefinition>())
        {
            if (effect == null
                || !handlers.TryGetValue(
                    effect.GetType(),
                    out IOffenseDecisionEffectHandler handler))
            {
                throw new InvalidOperationException(
                    $"Cannot execute offense decision effect '{effect?.GetType().Name ?? "null"}'.");
            }

            handler.Execute(effect, context);
        }
    }
}

public sealed class OffenseSupplyDecisionEffectHandler :
    IOffenseDecisionEffectHandler
{
    public Type EffectType => typeof(OffenseSupplyDecisionEffect);

    public bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        OffenseSupplyDecisionEffect supply = (OffenseSupplyDecisionEffect)effect;
        if (supply.amount < 0
            && context.Expedition.Supplies.Get(supply.supplyType) < -supply.amount)
        {
            reason = $"{OffenseSupplyCatalog.GetDisplayName(supply.supplyType)}이 부족합니다.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context)
    {
        OffenseSupplyDecisionEffect supply = (OffenseSupplyDecisionEffect)effect;
        if (supply.amount < 0)
        {
            context.Expedition.Supplies.TryConsume(
                supply.supplyType,
                -supply.amount);
        }
        else
        {
            context.Expedition.Supplies.Add(supply.supplyType, supply.amount);
        }

        context.Results.Add(
            $"{OffenseSupplyCatalog.GetDisplayName(supply.supplyType)} "
            + $"{(supply.amount >= 0 ? "+" : string.Empty)}{supply.amount}");
    }
}

public sealed class OffenseGoldDecisionEffectHandler :
    IOffenseDecisionEffectHandler
{
    public Type EffectType => typeof(OffenseGoldDecisionEffect);

    public bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        OffenseGoldDecisionEffect gold =
            (OffenseGoldDecisionEffect)effect;
        int amount = gold.amount;
        bool useFieldFunds = context.Expedition.UsesWorldTravel;
        if (amount < 0
            && !(useFieldFunds
                ? context.Expedition.CanSpendFieldFunds(-amount)
                : context.Money.CanSpend(-amount)))
        {
            reason = useFieldFunds
                ? "배정한 현장 자금이 부족합니다."
                : "골드가 부족합니다.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context)
    {
        OffenseGoldDecisionEffect gold =
            (OffenseGoldDecisionEffect)effect;
        int amount = gold.amount;
        bool useFieldFunds = context.Expedition.UsesWorldTravel;
        if (amount < 0)
        {
            bool spent = useFieldFunds
                ? context.Expedition.TrySpendFieldFunds(-amount)
                : context.Money.TrySpend(
                    -amount,
                    new EconomyTransactionContext(
                        gold.bribe?.IsValid == true
                            ? EconomyTransactionKind.Bribe
                            : EconomyTransactionKind.LegacyExpense,
                        context.Expedition.ExpeditionId,
                        gold.bribe?.offerId ?? string.Empty,
                        gold.bribe?.IsValid == true
                            ? "원정 현장 뇌물"
                            : "원정 현장 지출"),
                    out _);
            if (!spent)
            {
                throw new InvalidOperationException(
                    useFieldFunds
                        ? "배정한 현장 자금이 부족합니다."
                        : "골드가 부족합니다.");
            }
        }
        else
        {
            if (useFieldFunds)
            {
                context.Expedition.AddFieldFunds(amount);
            }
            else
            {
                context.Money.Add(amount);
            }
        }

        context.Results.Add(
            $"{(useFieldFunds ? "현장 자금" : "골드")} "
            + $"{(amount >= 0 ? "+" : string.Empty)}{amount}");
        ResolveBribe(gold.bribe, context);
    }

    private static void ResolveBribe(
        BribeOffer offer,
        OffenseDecisionEffectContext context)
    {
        if (offer?.IsValid != true)
        {
            return;
        }

        if (!offer.IsAccepted(context.DeterministicRoll))
        {
            context.Results.Add(
                string.IsNullOrWhiteSpace(offer.rejectedResult)
                    ? "상대가 뇌물을 거절하고 요구 조건을 바꿨습니다."
                    : offer.rejectedResult);
            return;
        }

        switch (offer.outcome)
        {
            case BribeOutcomeKind.Passage:
                context.Travel.TryAdjustExposure(
                    context.Expedition.ExpeditionId,
                    -12f,
                    out _);
                break;
            case BribeOutcomeKind.HostilityDelay:
                context.Travel.TryAdjustExposure(
                    context.Expedition.ExpeditionId,
                    -8f,
                    out _);
                break;
            case BribeOutcomeKind.RiskReduction:
                context.Travel.TryAdjustExposure(
                    context.Expedition.ExpeditionId,
                    -18f,
                    out _);
                break;
            case BribeOutcomeKind.InformationPurchase:
                RevealNearestHiddenSite(context);
                break;
        }

        context.Results.Add(
            string.IsNullOrWhiteSpace(offer.acceptedResult)
                ? "거래가 성립했습니다."
                : offer.acceptedResult);
    }

    private static void RevealNearestHiddenSite(
        OffenseDecisionEffectContext context)
    {
        OffenseHexCoord origin = context.Travel.TryGetState(
            context.Expedition.ExpeditionId,
            out OffenseTravelStateData travel)
            ? travel.CurrentCoord
            : context.World.DungeonCoord;
        OffenseWorldSiteStateData site = context.World.Sites
            .Where(candidate => candidate != null
                && candidate.state == OffenseWorldSiteState.Hidden)
            .OrderBy(candidate => origin.DistanceTo(candidate.Coord))
            .ThenBy(candidate => candidate.siteId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (site != null)
        {
            context.World.TryRevealSite(site.siteId);
        }
    }
}

public sealed class OffenseStressDecisionEffectHandler :
    IOffenseDecisionEffectHandler
{
    public Type EffectType => typeof(OffenseStressDecisionEffect);

    public bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        reason = string.Empty;
        return true;
    }

    public void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context)
    {
        float amount = ((OffenseStressDecisionEffect)effect).amount;
        context.Expedition.AdjustStress(amount);
        context.Results.Add(
            amount >= 0f ? "원정대 스트레스 증가" : "원정대 스트레스 감소");
    }
}

public sealed class OffenseExposureDecisionEffectHandler :
    IOffenseDecisionEffectHandler
{
    public Type EffectType => typeof(OffenseExposureDecisionEffect);

    public bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        reason = string.Empty;
        return true;
    }

    public void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context)
    {
        float amount = ((OffenseExposureDecisionEffect)effect).amount;
        context.Travel.TryAdjustExposure(
            context.Expedition.ExpeditionId,
            amount,
            out float exposure);
        context.Results.Add($"노출도 {exposure:0}");
    }
}

public sealed class OffenseInjuryDecisionEffectHandler :
    IOffenseDecisionEffectHandler
{
    public Type EffectType => typeof(OffenseInjuryDecisionEffect);

    public bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        reason = context.Expedition.MemberStates.Any(member => member.IsAlive)
            ? string.Empty
            : "행동 가능한 원정대원이 없습니다.";
        return string.IsNullOrEmpty(reason);
    }

    public void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context)
    {
        OffenseInjuryDecisionEffect injury = (OffenseInjuryDecisionEffect)effect;
        if (injury.maxHealthRatio >= 0f)
        {
            context.Expedition.ApplyEventInjury(
                injury.maxHealthRatio,
                context.DeterministicRoll,
                injury.nonLethal);
            context.Results.Add("원정대원 부상");
        }
        else
        {
            context.Expedition.HealMostInjured(-injury.maxHealthRatio);
            context.Results.Add("부상 회복");
        }
    }
}

public sealed class OffenseLootDecisionEffectHandler :
    IOffenseDecisionEffectHandler
{
    public Type EffectType => typeof(OffenseLootDecisionEffect);

    public bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        OffenseLootDecisionEffect loot = (OffenseLootDecisionEffect)effect;
        if (loot.amount < 0
            && context.Expedition.GetCarriedStock(loot.stockCategory) < -loot.amount)
        {
            reason = "포기할 전리품이 부족합니다.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context)
    {
        OffenseLootDecisionEffect loot = (OffenseLootDecisionEffect)effect;
        if (loot.amount >= 0)
        {
            context.Expedition.AddCarriedLoot(loot.stockCategory, loot.amount);
        }
        else
        {
            context.Expedition.TryRemoveCarriedLoot(
                loot.stockCategory,
                -loot.amount);
        }

        context.Results.Add(
            $"전리품 {(loot.amount >= 0 ? "+" : string.Empty)}{loot.amount}");
    }
}

public sealed class OffenseReconDecisionEffectHandler :
    IOffenseDecisionEffectHandler
{
    public Type EffectType => typeof(OffenseReconDecisionEffect);

    public bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        reason = string.Empty;
        return true;
    }

    public void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context)
    {
        int count = Mathf.Max(1, ((OffenseReconDecisionEffect)effect).revealCount);
        OffenseHexCoord origin = context.Travel.TryGetState(
            context.Expedition.ExpeditionId,
            out OffenseTravelStateData travel)
            ? travel.CurrentCoord
            : context.World.DungeonCoord;
        int revealed = 0;
        foreach (OffenseWorldSiteStateData site in context.World.Sites
                     .Where(site => site != null
                         && site.state == OffenseWorldSiteState.Hidden)
                     .OrderBy(site => origin.DistanceTo(site.Coord))
                     .ThenBy(site => site.siteId, StringComparer.Ordinal))
        {
            if (context.World.TryRevealSite(site.siteId))
            {
                revealed++;
            }

            if (revealed >= count)
            {
                break;
            }
        }

        context.Results.Add(
            revealed > 0 ? $"숨은 거점 {revealed}곳 발견" : "새로운 거점 정보 없음");
    }
}

public sealed class OffenseTimeDecisionEffectHandler :
    IOffenseDecisionEffectHandler
{
    public Type EffectType => typeof(OffenseTimeDecisionEffect);

    public bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        reason = string.Empty;
        return true;
    }

    public void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context)
    {
        float hours = Mathf.Max(
            0f,
            ((OffenseTimeDecisionEffect)effect).elapsedHours);
        context.World.AdvanceHours(hours);
        context.Results.Add($"원정 시간 +{hours:0.#}시간");
    }
}

public sealed class OffenseEquipmentWearDecisionEffectHandler :
    IOffenseDecisionEffectHandler
{
    public Type EffectType => typeof(OffenseEquipmentWearDecisionEffect);

    public bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        reason = string.Empty;
        return true;
    }

    public void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context)
    {
        if (context.Equipment == null)
        {
            context.Results.Add("장비 손상 없음");
            return;
        }

        float damage = Mathf.Max(
            0f,
            ((OffenseEquipmentWearDecisionEffect)effect).durabilityDamage);
        List<string> candidates = new List<string>();
        foreach (OffenseExpeditionMemberState member in
                 context.Expedition.MemberStates.Where(member =>
                     member.IsAlive && member.Actor != null))
        {
            string characterId =
                member.Actor.Identity?.PersistentId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(characterId))
            {
                continue;
            }

            candidates.AddRange(context.Equipment
                .GetArmor(characterId)
                .Select(armor => armor.InstanceId)
                .Where(id => !string.IsNullOrWhiteSpace(id)));
            CombatShieldSnapshot shield = context.Equipment.GetShield(characterId);
            if (shield.IsValid)
            {
                candidates.Add(shield.InstanceId);
            }
        }

        string[] ordered = candidates
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0)
        {
            context.Results.Add("손상될 방어 장비 없음");
            return;
        }

        int index = (int)((uint)context.DeterministicRoll
            % (uint)ordered.Length);
        context.Equipment.TryApplyDurabilityDamage(ordered[index], damage);
        context.Results.Add("방어 장비 내구도 감소");
    }
}

public sealed class OffenseForcedMoveDecisionEffectHandler :
    IOffenseDecisionEffectHandler
{
    public Type EffectType => typeof(OffenseForcedMoveDecisionEffect);

    public bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        reason = string.Empty;
        return true;
    }

    public void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context)
    {
        context.ForcesMovement = true;
    }
}

public sealed class OffenseCombatDecisionEffectHandler :
    IOffenseDecisionEffectHandler
{
    public Type EffectType => typeof(OffenseCombatDecisionEffect);

    public bool CanExecute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context,
        out string reason)
    {
        reason = string.Empty;
        return true;
    }

    public void Execute(
        OffenseDecisionEffectDefinition effect,
        OffenseDecisionEffectContext context)
    {
        context.StartsCombat = true;
    }
}
